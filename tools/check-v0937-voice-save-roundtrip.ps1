param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$binRoot = Join-Path $projectRoot ".godot\mono\temp\bin\$Configuration"
$requiredAssemblies = @(
    "GodotSharp.dll",
    "0Harmony.dll",
    "sts2.dll",
    "ShinGetterMod.dll"
)

foreach ($assemblyName in $requiredAssemblies) {
    $assemblyPath = Join-Path $binRoot $assemblyName
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Missing $assemblyName. Run dotnet build before this check."
    }
}

$modAssemblyPath = Join-Path $binRoot "ShinGetterMod.dll"
$latestSourceWriteTime = Get-ChildItem -LiteralPath (Join-Path $projectRoot "src") -Recurse -Filter "*.cs" -File |
    Measure-Object -Property LastWriteTimeUtc -Maximum |
    Select-Object -ExpandProperty Maximum
if ((Get-Item -LiteralPath $modAssemblyPath).LastWriteTimeUtc -lt $latestSourceWriteTime) {
    throw "ShinGetterMod.dll is older than the C# sources. Run dotnet build before this check."
}

[Reflection.Assembly]::LoadFrom((Join-Path $binRoot "GodotSharp.dll")) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $binRoot "0Harmony.dll")) | Out-Null
$gameAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $binRoot "sts2.dll"))
$modAssembly = [Reflection.Assembly]::LoadFrom($modAssemblyPath)

$cacheType = $gameAssembly.GetType(
    "MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache",
    $true)
$savedPropertiesType = $gameAssembly.GetType(
    "MegaCrit.Sts2.Core.Saves.Runs.SavedProperties",
    $true)
$abstractModelType = $gameAssembly.GetType(
    "MegaCrit.Sts2.Core.Models.AbstractModel",
    $true)
$injectType = $cacheType.GetMethod("InjectTypeIntoCache")
$fromInternal = $savedPropertiesType.GetMethod(
    "FromInternal",
    [Reflection.BindingFlags]"Public,Static")
$isMutable = $abstractModelType.GetProperty(
    "IsMutable",
    [Reflection.BindingFlags]"Instance,Public,NonPublic")

$expectedMask = 4194305
$expectedCombatStartVoiceCount = 7
$relicTypeNames = @(
    "ShinGetterMod.Models.Relics.SGR_GetterFurnace",
    "ShinGetterMod.Models.Relics.SGR_EmperorsFragment"
)

foreach ($relicTypeName in $relicTypeNames) {
    $relicType = $modAssembly.GetType($relicTypeName, $true)
    $injectType.Invoke($null, @($relicType)) | Out-Null

    $maskField = $relicType.GetField(
        "_playedVoiceMask",
        [Reflection.BindingFlags]"Instance,NonPublic")
    $combatStartCountField = $relicType.GetField(
        "_combatStartVoiceCount",
        [Reflection.BindingFlags]"Instance,NonPublic")
    $source = [Activator]::CreateInstance($relicType)
    $maskField.SetValue($source, $expectedMask)
    $combatStartCountField.SetValue($source, $expectedCombatStartVoiceCount)

    $saved = $fromInternal.Invoke($null, @($source, $null))
    if ($null -eq $saved -or $saved.ints.Count -ne 2) {
        throw "$relicTypeName did not serialize both voice int properties."
    }
    $savedMask = $saved.ints | Where-Object name -eq "PlayedVoiceMask" | Select-Object -First 1
    $savedCombatStartCount = $saved.ints | Where-Object name -eq "CombatStartVoiceCount" | Select-Object -First 1
    if ($null -eq $savedMask -or $savedMask.value -ne $expectedMask) {
        throw "$relicTypeName serialized an unexpected voice mask value."
    }
    if ($null -eq $savedCombatStartCount -or $savedCombatStartCount.value -ne $expectedCombatStartVoiceCount) {
        throw "$relicTypeName serialized an unexpected combat-start voice count."
    }

    $restored = [Activator]::CreateInstance($relicType)
    $isMutable.SetValue($restored, $true)
    $saved.FillInternal($restored)
    if ($maskField.GetValue($restored) -ne $expectedMask) {
        throw "$relicTypeName failed the PlayedVoiceMask save round-trip."
    }
    if ($combatStartCountField.GetValue($restored) -ne $expectedCombatStartVoiceCount) {
        throw "$relicTypeName failed the CombatStartVoiceCount save round-trip."
    }
}

Write-Host "PASSED voice SavedProperty round-trip checks."

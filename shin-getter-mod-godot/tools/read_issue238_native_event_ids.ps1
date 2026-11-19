param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$GodotSharp = '',
    [string]$Sts2ReferenceDir = $env:STS2_REFERENCE_DIR
)

# Read actual formal 0.111.1 identities only. No ModelDb.Init/Inject, model construction,
# mod assembly, game, scene, save or generated file is involved.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$config = Get-Content -LiteralPath (Join-Path $ProjectRoot 'tools/issue238-native-events.json') -Raw | ConvertFrom-Json
$reference = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot 'lib/sts2.dll'))
$hash = (Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash
if ($hash -cne $config.native_sha256) { throw "Unreviewed native DLL: $hash" }
if ([string]::IsNullOrWhiteSpace($Sts2ReferenceDir)) {
    $Sts2ReferenceDir = Split-Path -Parent $reference
}
$dependencyHash = (Get-FileHash -LiteralPath (Join-Path $Sts2ReferenceDir 'sts2.dll') -Algorithm SHA256).Hash
if ($dependencyHash -cne $hash) { throw "STS2_REFERENCE_DIR must match the compiled native DLL: $dependencyHash" }
if ($GodotSharp.Length -eq 0) {
    $GodotSharp = Join-Path $Sts2ReferenceDir 'GodotSharp.dll'
    if (!(Test-Path -LiteralPath $GodotSharp -PathType Leaf)) {
        $profileRoot = [Environment]::GetFolderPath('UserProfile')
        $GodotSharp = Join-Path $profileRoot ".nuget/packages/godotsharp/$($config.godot_sharp_version)/lib/net8.0/GodotSharp.dll"
    }
}
# The native module initializer can write to Console.Out; keep stdout machine-readable.
$originalOutput = [Console]::Out
$nativeOutput = [System.IO.StringWriter]::new()
try {
    [Console]::SetOut($nativeOutput)
    $null = [System.Reflection.Assembly]::LoadFrom($GodotSharp)
    foreach ($name in @('System.IO.Hashing.dll', 'Sentry.dll', 'Sentry.Godot.dll')) {
        $dependency = Join-Path $Sts2ReferenceDir $name
        if (!(Test-Path -LiteralPath $dependency -PathType Leaf)) {
            throw "Missing $name; set STS2_REFERENCE_DIR to the matching official game's assembly directory."
        }
        $null = [System.Reflection.Assembly]::LoadFrom($dependency)
    }
    $native = [System.Reflection.Assembly]::LoadFrom($reference)
    $db = $native.GetType('MegaCrit.Sts2.Core.Models.ModelDb', $true)
    $eventBase = $native.GetType('MegaCrit.Sts2.Core.Models.EventModel', $true)
    $getId = $db.GetMethod('GetId', [System.Reflection.BindingFlags]'Public,Static', $null, [Type[]]@([System.Type]), $null)
    if ($null -eq $getId -or !$getId.IsPublic -or !$getId.IsStatic) { throw 'Native public ModelDb.GetId(Type) unavailable' }
    $rows = @(foreach ($entry in $config.events) {
        $type = $native.GetType('MegaCrit.Sts2.Core.Models.Events.' + $entry.type, $true)
        if (!$type.IsSubclassOf($eventBase)) { throw "Not a native EventModel: $type" }
        $id = $getId.Invoke($null, [object[]]@($type))
        if ($id.Category -cne 'EVENT') { throw "Unexpected event category: $id" }
        [pscustomobject]@{ type = $entry.type; category = $id.Category; entry = $id.Entry }
    })
} finally {
    [Console]::SetOut($originalOutput)
    [Console]::Error.Write($nativeOutput.ToString())
    $nativeOutput.Dispose()
}
if ($rows.Count -ne 15 -or @($rows.entry | Sort-Object -Unique).Count -ne 15) { throw 'Expected 15 unique native event identities' }
[pscustomobject]@{
    native_sha256 = $hash
    method = 'actual ModelDb.GetId(Type)'
    rows = $rows
    registered_lookup = 'NOT_RUN'
    game_started = $false
    mod_dll_loaded = $false
} | ConvertTo-Json -Depth 6 -Compress

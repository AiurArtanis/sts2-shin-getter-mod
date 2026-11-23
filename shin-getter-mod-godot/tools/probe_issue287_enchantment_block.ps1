# Requires a built mod DLL and matching official game assemblies. No game processes or saves.
param(
    [Parameter(Mandatory)][string]$GameAssemblies,
    [Parameter(Mandatory)][string]$ModDll
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$gameDll = Join-Path $GameAssemblies 'sts2.dll'
$sha = (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash
$hasNewApi = switch ($sha) {
    'A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7' { $false }
    '62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3' { $true }
    default { throw "Unreviewed official DLL: $sha" }
}
foreach ($name in 'GodotSharp.dll', 'System.IO.Hashing.dll', 'Sentry.dll', 'Sentry.Godot.dll', '0Harmony.dll') {
    $null = [Reflection.Assembly]::LoadFrom((Join-Path $GameAssemblies $name))
}
$game = [Reflection.Assembly]::LoadFrom($gameDll)
$mod = [Reflection.Assembly]::LoadFrom($ModDll)
$cardType = $game.GetType('MegaCrit.Sts2.Core.Models.CardModel', $true)
$enchantmentType = $game.GetType('MegaCrit.Sts2.Core.Models.EnchantmentModel', $true)
$animation = $mod.GetType('ShinGetterMod.Patches.ShinGetterCardPlayVoicePatch', $true)
$stretch = $game.GetType('MegaCrit.Sts2.Core.Models.Powers.StretchPower', $true)
$staticPrivate = [Reflection.BindingFlags]'Static,NonPublic'
$getter = $enchantmentType.GetProperty('GainsBlock')
$stretchClassifier = $stretch.GetMethod('GainsBlock', $staticPrivate)
$animationClassifier = $animation.GetMethod('AnimationTriggerFor', $staticPrivate)
if (($null -ne $getter) -ne $hasNewApi -or ($null -ne $stretchClassifier) -ne $hasNewApi) {
    throw 'Native Block capability does not match the pinned version'
}
if ($null -eq $animationClassifier) { throw 'Mod animation classifier unavailable' }

$adroit = [Activator]::CreateInstance($game.GetType('MegaCrit.Sts2.Core.Models.Enchantments.Adroit', $true))
$adaptation = [Activator]::CreateInstance($mod.GetType('ShinGetterMod.Models.Enchantments.SGE_Adaptation', $true))
$devolution = [Activator]::CreateInstance($mod.GetType('ShinGetterMod.Models.Enchantments.SGE_Devolution', $true))
if ($hasNewApi -and ($getter.GetValue($adaptation) -or $getter.GetValue($devolution))) {
    throw 'Getter enchantments must not claim to grant Block'
}
$enchantmentProperty = $cardType.GetProperty('Enchantment')
$enchantedSkill = if ($hasNewApi) { 'Block' } else { 'Cast' }
$cases = @(
    @{ Card='Acrobatics'; Enchantment=$null; Trigger='Cast'; CountsBlock=$false },
    @{ Card='Acrobatics'; Enchantment=$adroit; Trigger=$enchantedSkill; CountsBlock=$hasNewApi },
    @{ Card='Acrobatics'; Enchantment=$adaptation; Trigger='Cast'; CountsBlock=$false },
    @{ Card='DefendSilent'; Enchantment=$adaptation; Trigger='Block'; CountsBlock=$true },
    @{ Card='StrikeSilent'; Enchantment=$adroit; Trigger='Attack'; CountsBlock=$hasNewApi },
    @{ Card='StrikeSilent'; Enchantment=$devolution; Trigger='Attack'; CountsBlock=$false },
    @{ Card='Footwork'; Enchantment=$null; Trigger='Cast'; CountsBlock=$false },
    @{ Card='Dazed'; Enchantment=$null; Trigger='Dash'; CountsBlock=$false }
)
foreach ($case in $cases) {
    $card = [Activator]::CreateInstance($game.GetType("MegaCrit.Sts2.Core.Models.Cards.$($case.Card)", $true))
    # Supply the final card association; only classifiers are exercised, not enchantment application.
    $enchantmentProperty.SetValue($card, $case.Enchantment)
    $counts = if ($hasNewApi) { $stretchClassifier.Invoke($null, @($card)) } else { $card.GainsBlock }
    $trigger = $animationClassifier.Invoke($null, @($card))
    $enchantmentName = if ($null -ne $case.Enchantment) { $case.Enchantment.GetType().Name } else { 'none' }
    "$($case.Card)/${enchantmentName}: trigger=$trigger expected=$($case.Trigger); nativeBlock=$counts"
    if ($counts -ne $case.CountsBlock -or $trigger -ne $case.Trigger) {
        throw 'Unexpected native Block classification or mod animation trigger'
    }
}
"PASS: $($cases.Count) cases; native enchantment capability=$hasNewApi; no game started"

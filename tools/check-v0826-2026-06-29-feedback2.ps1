$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path
}

function Require-File([string]$name, [string]$relativePath) {
    if (!(Test-Path -LiteralPath (Join-Path $root $relativePath))) {
        $failures.Add($name)
    }
}

function Require-Pattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) {
        $failures.Add($name)
    }
}

function Require-AbsentPattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -ne $text -and $text -match $pattern) {
        $failures.Add($name)
    }
}

function Get-JsonValue($json, [string]$key) {
    return $json.PSObject.Properties[$key].Value
}

$powers = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'ShinGetterMod\localization\zhs\powers.json') | ConvertFrom-Json
$amountPowerKeys = @(
    'S_G_P_FIGHTING_SPIRIT.description',
    'S_G_P_EVOLUTION_ENGINE.description',
    'S_G_P_TRIPLE_UNITY.description',
    'S_G_P_INDOMITABLE.description',
    'S_G_P_ACCELERATION.description',
    'S_G_P_CHOSEN_ONE.description',
    'S_G_P_WARRIOR_MEDAL.description',
    'S_G_P_GRAPPLE.description',
    'S_G_P_DESPERATION.description',
    'S_G_P_BLUEPRINT.description',
    'S_G_P_AWAKENED_SOUL.description',
    'S_G_P_INSIGHT.description',
    'S_G_P_INFINITE_EVOLUTION.description',
    'S_G_P_OVERLOAD.description',
    'S_G_P_CHAIN_REACTION.description',
    'S_G_P_IRON_WALL.description',
    'S_G_P_TOMAHAWK.description',
    'S_G_P_DARK_CAPE.description',
    'S_G_P_ENABLE.description'
)

foreach ($key in $amountPowerKeys) {
    $value = Get-JsonValue $powers $key
    if ($null -eq $value -or $value -notmatch '\{Amount\}') {
        $failures.Add("Power description uses Amount: $key")
    }
}

$radiationDescription = Get-JsonValue $powers 'S_G_P_RADIATION.description'
if ($null -eq $radiationDescription -or $radiationDescription -notmatch '\{DamageIncreasePercent\}') {
    $failures.Add('Radiation description shows total damage increase percent')
}

Require-Pattern 'Radiation injects DamageIncreasePercent into Description' 'src\Models\Powers\SGP_Radiation.cs' 'Description[\s\S]*DamageIncreasePercent[\s\S]*Amount \* 25m'

Require-Pattern 'Seal allows its own countdown through' 'src\Models\Powers\SGP_Seal.cs' 'canonicalPower is SGP_Seal'
Require-Pattern 'Seal only affects its owner' 'src\Models\Powers\SGP_Seal.cs' 'target != Owner'
Require-Pattern 'Seal blocks nonzero visible power amount changes' 'src\Models\Powers\SGP_Seal.cs' 'amount != 0m[\s\S]*canonicalPower\.IsVisible'
Require-AbsentPattern 'Seal no longer exempts Regen' 'src\Models\Powers\SGP_Seal.cs' 'RegenPower'

Require-AbsentPattern 'Desperation card no longer has retain or exhaust keywords' 'src\Models\Cards\SGC_Desperation.cs' 'CardKeyword\.(Retain|Exhaust)'
Require-Pattern 'Desperation card gains 3 Ki' 'src\Models\Cards\SGC_Desperation.cs' 'new PowerVar<SGP_Ki>\(3m\)[\s\S]*PowerCmd\.Apply<SGP_Ki>'
Require-AbsentPattern 'Desperation power no longer makes spirit cards free' 'src\Models\Powers\SGP_Desperation.cs' 'TryModifyEnergyCostInCombatLate|_spiritCardsAreFree'

Require-AbsentPattern 'Shin Form card is no longer ethereal' 'src\Models\Cards\SGC_ShinForm.cs' 'CardKeyword\.Ethereal'
Require-Pattern 'Shin Form card costs 4 and is a skill' 'src\Models\Cards\SGC_ShinForm.cs' 'base\(4, CardType\.Skill'
Require-Pattern 'Shin Form card discounts from evolution memory' 'src\Models\Cards\SGC_ShinForm.cs' 'TryModifyEnergyCostInCombat[\s\S]*SGP_EvolutionMemory'
Require-File 'Evolution memory power exists' 'src\Models\Powers\SGP_EvolutionMemory.cs'
Require-Pattern 'Evolution memory is invisible' 'src\Models\Powers\SGP_EvolutionMemory.cs' 'IsVisibleInternal => false'
Require-Pattern 'Evolution records positive gained amount' 'src\Models\Powers\SGP_Evolution.cs' 'AfterPowerAmountChanged[\s\S]*power != this[\s\S]*amount <= 0m[\s\S]*SGP_EvolutionMemory'

Require-File 'Getter ray rich text effect exists' 'src\RichTextTags\RichTextGetterRay.cs'
Require-Pattern 'Getter ray rich text tag uses getter_ray bbcode' 'src\RichTextTags\RichTextGetterRay.cs' 'Bbcode => "getter_ray"'
Require-Pattern 'Getter ray rich text tag uses getter ray color' 'src\RichTextTags\RichTextGetterRay.cs' '44FCC5'
Require-Pattern 'Getter ray rich text effect is registered' 'src\Patches\RichTextWhitePatch.cs' 'RichTextGetterRay'

$cardsText = Read-RepoFile 'ShinGetterMod\localization\zhs\cards.json'
$libraryText = Read-RepoFile 'ShinGetterMod\localization\zhs\card_library.json'
if (($cardsText + $libraryText) -match '\[cyan\]') {
    $failures.Add('No cyan rich text tags remain in card localization')
}
if (($cardsText + $libraryText) -notmatch '\[getter_ray\]') {
    $failures.Add('Getter ray rich text tags are used in card localization')
}

if ($failures.Count -gt 0) {
    Write-Host 'RED: 2026-06-29 feedback2 checks failing:'
    $failures | Select-Object -First 40
    exit 1
}

Write-Host 'GREEN: 2026-06-29 feedback2 checks passed.'

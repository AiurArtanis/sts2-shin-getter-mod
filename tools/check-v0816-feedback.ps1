$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

$getterThree = -join [char[]](0x4E09, 0x53F7, 0x673A)
$selfDamage = -join [char[]](0x5BF9, 0x81EA, 0x5DF1, 0x9020, 0x6210, 0x4F24, 0x5BB3, 0x65F6, 0x83B7, 0x5F97, 0x7B49, 0x91CF)
$block = -join [char[]](0x683C, 0x6321)
$spiritPrefix = -join [char[]](0x3010, 0x6C14, 0x529B)

$checks = @(
    @{
        Name = 'Tomahawk Fury damage is 3'
        Path = 'src\Models\Cards\SGC_TomahawkFury.cs'
        Pattern = 'new DamageVar\(3m'
    },
    @{
        Name = 'Getter One grants Vigor'
        Path = 'src\Models\Powers\SGP_ShinGetterOne.cs'
        Pattern = 'PowerCmd\.Apply<VigorPower>'
    },
    @{
        Name = 'Getter One no longer grants Ki'
        Path = 'src\Models\Powers\SGP_ShinGetterOne.cs'
        Absent = 'PowerCmd\.Apply<SGP_Ki>'
    },
    @{
        Name = 'Getter Two grants Regen on transform'
        Path = 'src\Models\Powers\SGP_ShinGetterTwo.cs'
        Pattern = 'AfterApplied[\s\S]*PowerCmd\.Apply<RegenPower>'
    },
    @{
        Name = 'Getter Three grants Plating on transform'
        Path = 'src\Models\Powers\SGP_ShinGetterThree.cs'
        Pattern = 'AfterApplied[\s\S]*PowerCmd\.Apply<PlatingPower>[\s\S]*NShinGetterStaticVisuals\.ShowForm'
    },
    @{
        Name = 'Ki no longer decays at turn end'
        Path = 'src\Models\Powers\SGP_Ki.cs'
        Absent = 'AfterSideTurnEnd'
    },
    @{
        Name = 'Ki reduces final damage additively'
        Path = 'src\Models\Powers\SGP_Ki.cs'
        Pattern = 'ModifyDamageAdditive'
    },
    @{
        Name = 'Ki decrements after damage received'
        Path = 'src\Models\Powers\SGP_Ki.cs'
        Pattern = 'AfterDamageReceived'
    },
    @{
        Name = 'Ki decrements only after unblocked damage'
        Path = 'src\Models\Powers\SGP_Ki.cs'
        Pattern = 'result\.UnblockedDamage <= 0'
    },
    @{
        Name = 'Getter Two only halves card-sourced block'
        Path = 'src\Models\Powers\SGP_ShinGetterTwo.cs'
        Pattern = 'cardSource != null \|\| cardPlay != null'
    },
    @{
        Name = 'Getter Tomahawk autoplay power removes itself'
        Path = 'src\Models\Powers\SGP_Tomahawk.cs'
        Pattern = 'PowerCmd\.Remove\(this\)'
    },
    @{
        Name = 'Radiation ignores HP loss damage props'
        Path = 'src\Models\Powers\SGP_Radiation.cs'
        Pattern = 'IsHpLoss\(props\)'
    },
    @{
        Name = 'Specialization Getter2 effect grants 4 Regen'
        Path = 'src\Models\Cards\SGC_Specialization.cs'
        Pattern = 'PowerCmd\.Apply<RegenPower>[\s\S]*4m'
    },
    @{
        Name = 'Specialization Getter2 effect no longer gains energy'
        Path = 'src\Models\Cards\SGC_Specialization.cs'
        Absent = 'PlayerCmd\.GainEnergy\(1, Owner\)'
    },
    @{
        Name = 'Specialization Getter2 effect no longer draws a card'
        Path = 'src\Models\Cards\SGC_Specialization.cs'
        Absent = 'CardPileCmd\.Draw\(choiceContext, 1, Owner\)'
    },
    @{
        Name = 'Evolution consumes all evolution'
        Path = 'src\Models\Powers\SGP_Evolution.cs'
        Pattern = 'ModifyAmount\(choiceContext, this, -evolutionAmount'
    },
    @{
        Name = 'Evolution uses independent vigor cap'
        Path = 'src\Models\Powers\SGP_Evolution.cs'
        Pattern = 'strengthGain = Math\.Min\(vigorAmount, evolutionAmount\)'
    },
    @{
        Name = 'Getter Missile has Getter3 block description'
        Path = 'ShinGetterMod\localization\zhs\cards.json'
        Pattern = "$getterThree.*$selfDamage.*$block"
    },
    @{
        Name = 'Spirit description prefix removed'
        Path = 'src\Patches\SpiritRequirementDescriptionPatch.cs'
        Absent = $spiritPrefix
    },
    @{
        Name = 'Spirit card UI patch exists'
        Path = 'src\Patches\SpiritRequirementCardUiPatch.cs'
        Pattern = 'ShinGetterSpiritRequirementIcons'
    },
    @{
        Name = 'Card PNG exporter exists'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'
        Pattern = 'public static class ShinGetterCardPngExporter'
    },
    @{
        Name = 'Card PNG exporter uses direct NCard scene instancing'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'
        Pattern = 'PreloadManager\.Cache\.GetScene\(CardScenePath\)\s*[\r\n ]*\.Instantiate<NCard>'
    },
    @{
        Name = 'Card PNG exporter does not use shared NCard pool'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'
        Absent = 'NCard\.Create'
    },
    @{
        Name = 'Card export console command uses export_cards'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardExportConsoleCmd.cs'
        Pattern = 'CmdName\s*=>\s*"export_cards"'
    },
    @{
        Name = 'Card export console command parses quoted args'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardExportConsoleCmd.cs'
        Pattern = 'TryParseQuotedCommandArgs'
    },
    @{
        Name = 'Card exporter supports wildcard id filters'
        Path = 'src\Diagnostics\CardExport\ShinGetterCardPngExporter.cs'
        Pattern = 'WildcardToRegex'
    },
    @{
        Name = 'Card atlas strike coordinate updated'
        Path = 'images\atlases\card_atlas.sprites\shin_getter\s_g_c_strike.tres'
        Pattern = 'region = Rect2\(2, 2, 250, 190\)'
    },
    @{
        Name = 'Card atlas Getter Tomahawk coordinate updated'
        Path = 'images\atlases\card_atlas.sprites\shin_getter\s_g_c_getter_tomahawk.tres'
        Pattern = 'region = Rect2\(2, 194, 250, 190\)'
    },
    @{
        Name = 'Getter Flash has innate and exhaust'
        Path = 'src\Models\Cards\SGC_GetterFlash.cs'
        Pattern = 'CardKeyword\.Innate[\s\S]*CardKeyword\.Exhaust'
    },
    @{
        Name = 'Getter Flash damage is 5'
        Path = 'src\Models\Cards\SGC_GetterFlash.cs'
        Pattern = 'new DamageVar\(5m'
    },
    @{
        Name = 'Getter Flash no longer discounts Getter hand cards'
        Path = 'src\Models\Cards\SGC_GetterFlash.cs'
        Absent = 'AddThisTurnOrUntilPlayed'
    },
    @{
        Name = 'Getter Flash gains vigor equal to unblocked damage dealt'
        Path = 'src\Models\Cards\SGC_GetterFlash.cs'
        Pattern = 'damageDealt[\s\S]*UnblockedDamage[\s\S]*PowerCmd\.Apply<VigorPower>[\s\S]*damageDealt'
    },
    @{
        Name = 'Getter Flash Getter1 bonus vigor happens after damage'
        Path = 'src\Models\Cards\SGC_GetterFlash.cs'
        Pattern = 'var attack = await DamageCmd\.Attack[\s\S]*HasForm\(Owner, ShinGetterForm\.Getter1\)[\s\S]*PowerCmd\.Apply<VigorPower>[\s\S]*8m'
    }
)

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($check in $checks) {
    $path = Join-Path $root $check.Path
    $text = if (Test-Path $path) { Get-Content -Raw -Encoding UTF8 $path } else { '' }

    if ($check.Pattern -and ($text -notmatch $check.Pattern)) {
        $failures.Add($check.Name)
    }

    if ($check.Absent -and ($text -match $check.Absent)) {
        $failures.Add($check.Name)
    }
}

$getterChopPath = Join-Path $root 'src\Models\Cards\SGC_GetterChop.cs'
$getterChopText = if (Test-Path $getterChopPath) { Get-Content -Raw -Encoding UTF8 $getterChopPath } else { '' }
$plunderIndex = $getterChopText.IndexOf('await PlunderShield(cardPlay);', [StringComparison]::Ordinal)
$damageIndex = $getterChopText.IndexOf('DamageCmd.Attack', [StringComparison]::Ordinal)
if ($plunderIndex -lt 0 -or $damageIndex -lt 0 -or $plunderIndex -gt $damageIndex) {
    $failures.Add('Getter Chop plunders shield before damage')
}

if ($failures.Count -gt 0) {
    Write-Host 'RED: V0.8.16 feedback checks failing:'
    $failures | Select-Object -First 30
    exit 1
}

Write-Host 'GREEN: V0.8.16 feedback checks passed.'

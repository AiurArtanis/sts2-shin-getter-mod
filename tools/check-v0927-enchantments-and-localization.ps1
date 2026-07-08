$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$message) {
    $failures.Add($message) | Out-Null
}

function Assert-File([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Failure "$label missing: $path"
        return $false
    }
    return $true
}

function Assert-Contains([string]$path, [string]$pattern, [string]$label) {
    if (-not (Assert-File $path $label)) {
        return
    }

    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    if ($text -notmatch $pattern) {
        Add-Failure "$label does not contain expected pattern: $pattern"
    }
}

function Get-JsonKeys([string]$path) {
    $json = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    return @($json.PSObject.Properties.Name | Sort-Object)
}

function Get-PngSize([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 24) {
        return $null
    }

    $width = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
    $height = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
    return [PSCustomObject]@{ Width = $width; Height = $height }
}

$adaptation = Join-Path $repo "src\Models\Enchantments\SGE_Adaptation.cs"
$devolution = Join-Path $repo "src\Models\Enchantments\SGE_Devolution.cs"

Assert-Contains $adaptation "class\s+SGE_Adaptation\s*:\s*EnchantmentModel" "SGE_Adaptation class"
Assert-Contains $adaptation "HasExtraCardText\s*=>\s*true" "SGE_Adaptation extra card text"
Assert-Contains $adaptation "CreatureCmd\.Damage\(\s*choiceContext,\s*Card\.Owner\.Creature,\s*1m" "SGE_Adaptation HP loss"
Assert-Contains $adaptation "PowerCmd\.Apply<VigorPower>" "SGE_Adaptation vigor gain"
Assert-Contains $adaptation "TypeForCurrentAmount\s*==\s*PowerType\.Debuff" "SGE_Adaptation debuff detection"
Assert-Contains $adaptation "PowerCmd\.Decrement" "SGE_Adaptation debuff decrement"

Assert-Contains $devolution "class\s+SGE_Devolution\s*:\s*EnchantmentModel" "SGE_Devolution class"
Assert-Contains $devolution "CanEnchantCardType\(CardType\s+cardType\)" "SGE_Devolution type restriction"
Assert-Contains $devolution "cardType\s*==\s*CardType\.Attack" "SGE_Devolution attack-only"
Assert-Contains $devolution "EnergyCost\.UpgradeBy\(-1\)" "SGE_Devolution cost reduction"
Assert-Contains $devolution "EnchantDamageMultiplicative" "SGE_Devolution damage hook"
Assert-Contains $devolution "return\s+0\.5m" "SGE_Devolution damage half"

$locRoot = Join-Path $repo "ShinGetterMod\localization"
$zhsDir = Join-Path $locRoot "zhs"
foreach ($lang in @("eng", "jpn")) {
    $langDir = Join-Path $locRoot $lang
    if (-not (Assert-File $langDir "$lang localization directory")) {
        continue
    }

    foreach ($zhsFile in Get-ChildItem -LiteralPath $zhsDir -Filter "*.json") {
        $target = Join-Path $langDir $zhsFile.Name
        if (-not (Assert-File $target "$lang localization file $($zhsFile.Name)")) {
            continue
        }

        $sourceKeys = Get-JsonKeys $zhsFile.FullName
        $targetKeys = Get-JsonKeys $target
        $missing = Compare-Object $sourceKeys $targetKeys | Where-Object { $_.SideIndicator -eq "<=" }
        $extra = Compare-Object $sourceKeys $targetKeys | Where-Object { $_.SideIndicator -eq "=>" }
        if ($missing) {
            Add-Failure "$lang $($zhsFile.Name) missing keys: $(@($missing.InputObject) -join ', ')"
        }
        if ($extra) {
            Add-Failure "$lang $($zhsFile.Name) extra keys: $(@($extra.InputObject) -join ', ')"
        }
    }
}

foreach ($lang in @("zhs", "eng", "jpn")) {
    $path = Join-Path $locRoot "$lang\enchantments.json"
    if (-not (Assert-File $path "$lang enchantments localization")) {
        continue
    }

    $keys = Get-JsonKeys $path
    foreach ($key in @(
        "S_G_E_ADAPTATION.title",
        "S_G_E_ADAPTATION.description",
        "S_G_E_ADAPTATION.extraCardText",
        "S_G_E_DEVOLUTION.title",
        "S_G_E_DEVOLUTION.description",
        "S_G_E_DEVOLUTION.extraCardText"
    )) {
        if ($keys -notcontains $key) {
            Add-Failure "$lang enchantments.json missing key: $key"
        }
    }
}

foreach ($icon in @("s_g_e_adaptation.png", "s_g_e_devolution.png")) {
    $path = Join-Path $repo "images\enchantments\$icon"
    if (-not (Assert-File $path "enchantment icon $icon")) {
        continue
    }

    $size = Get-PngSize $path
    if ($null -eq $size -or $size.Width -ne 64 -or $size.Height -ne 64) {
        Add-Failure "enchantment icon $icon must be 64x64, got $($size.Width)x$($size.Height)"
    }

    $importPath = "$path.import"
    if (Assert-File $importPath "enchantment icon import $icon.import") {
        $sourceFile = "source_file=`"res://images/enchantments/$icon`""
        Assert-Contains $importPath ([regex]::Escape($sourceFile)) "enchantment icon import source $icon"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "FAILED v0.9.27 enchantments/localization checks:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PASSED v0.9.27 enchantments/localization checks." -ForegroundColor Green

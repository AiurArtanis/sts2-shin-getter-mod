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

function Require-Pattern([string]$name, [string]$relativePath, [string]$pattern) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text -or $text -notmatch $pattern) {
        $failures.Add($name)
    }
}

function Require-JsonVersionAtLeast([string]$name, [string]$relativePath, [int]$major, [int]$minor, [int]$patch) {
    $text = Read-RepoFile $relativePath
    if ($null -eq $text) {
        $failures.Add($name)
        return
    }

    $json = $text | ConvertFrom-Json
    if ($json.version -notmatch '^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
        $failures.Add("$name (unparseable '$($json.version)')")
        return
    }

    $actual = [version]"$($Matches.major).$($Matches.minor).$($Matches.patch)"
    $minimum = [version]"$major.$minor.$patch"
    if ($actual -lt $minimum) {
        $failures.Add("$name (found '$($json.version)')")
    }
}

Require-Pattern 'Fighting Spirit base damage lowered to 5' 'src\Models\Cards\SGC_FightingSpirit.cs' 'new\s+DamageVar\(5m'
Require-Pattern 'Fighting Spirit upgrade remains +3 for 5 to 8 damage' 'src\Models\Cards\SGC_FightingSpirit.cs' 'UpgradeValueBy\(3m\)'
Require-Pattern 'Insight requires 3 spirit' 'src\Models\Cards\SGC_Insight.cs' 'SpiritRequirement\s*=>\s*3'

Require-JsonVersionAtLeast 'Root manifest version is at least v0.9.6' 'ShinGetterMod.json' 0 9 6
Require-JsonVersionAtLeast 'Build manifest version is at least v0.9.6' 'build\ShinGetterMod.json' 0 9 6

Require-Pattern 'Card export command has DevConsole fallback patch' 'src\Patches\ShinGetterCardExportConsolePatch.cs' 'HarmonyPatch\(typeof\(DevConsole\),\s*"ProcessCommand"'
Require-Pattern 'Card export fallback handles export_cards case-insensitively' 'src\Patches\ShinGetterCardExportConsolePatch.cs' 'export_cards[\s\S]*StringComparison\.OrdinalIgnoreCase'
Require-Pattern 'Card export fallback delegates to existing exporter command' 'src\Patches\ShinGetterCardExportConsolePatch.cs' 'new\s+ShinGetterCardExportConsoleCmd\(\)\.Process\(player,\s*args\)'
Require-Pattern 'Quoted export_cards arguments are still rejoined for paths with spaces' 'src\Diagnostics\CardExport\ShinGetterCardExportConsoleCmd.cs' 'string\.Join\(" ",\s*rawArgs\)'

Require-Pattern 'Deploy script copies artifacts through explicit deploy helper' 'build-and-deploy.ps1' 'function\s+Copy-DeployArtifact'
Require-Pattern 'Deploy script reports locked or occupied deploy files' 'build-and-deploy.ps1' 'locked by the running game|可能被游戏占用'
Require-Pattern 'Deploy script deploys manifest together with dll and pck' 'build-and-deploy.ps1' 'ShinGetterMod\.json[\s\S]*Copy-DeployArtifact'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.6 / 2026-07-01 feedback checks failing:'
    $failures | Select-Object -First 80
    exit 1
}

Write-Host 'GREEN: v0.9.6 / 2026-07-01 feedback checks passed.'

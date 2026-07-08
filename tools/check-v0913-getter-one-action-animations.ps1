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

function Require-FileCount([string]$name, [string]$relativePath, [string]$filter, [int]$expected) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path -LiteralPath $path)) {
        $failures.Add($name)
        return
    }

    $count = (Get-ChildItem -LiteralPath $path -File -Filter $filter | Measure-Object).Count
    if ($count -ne $expected) {
        $failures.Add("$name (expected $expected, got $count)")
    }
}

Require-Pattern 'Root manifest version is v0.9.13+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[3-9]|[2-9]\d)"'

Require-FileCount 'Getter One attack animation exports 40 png frames' 'images\characters\shin_getter\forms\getter_one_attack' '*.png' 40
Require-FileCount 'Getter One cast animation exports 32 png frames' 'images\characters\shin_getter\forms\getter_one_cast' '*.png' 32

$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
Require-Pattern 'Sprite sequence declares idle animation name' $sequencePath 'IdleAnimationName\s*=\s*"idle"'
Require-Pattern 'Sprite sequence declares attack animation name' $sequencePath 'AttackAnimationName\s*=\s*"attack"'
Require-Pattern 'Sprite sequence declares cast animation name' $sequencePath 'CastAnimationName\s*=\s*"cast"'
Require-Pattern 'Sprite sequence knows attack frame directory' $sequencePath 'getter_one_attack'
Require-Pattern 'Sprite sequence knows cast frame directory' $sequencePath 'getter_one_cast'
Require-Pattern 'Sprite sequence loads one-shot attack animation' $sequencePath 'LoadLinearAnimation[\s\S]*AttackAnimationName[\s\S]*loop:\s*false'
Require-Pattern 'Sprite sequence loads one-shot cast animation' $sequencePath 'LoadLinearAnimation[\s\S]*CastAnimationName[\s\S]*loop:\s*false'
Require-Pattern 'Sprite sequence keeps idle ping-pong loop' $sequencePath 'LoadPingPongAnimation[\s\S]*IdleAnimationName[\s\S]*loop:\s*true'

$stateMachinePath = 'src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs'
Require-Pattern 'Getter One sprite animation state machine exists' $stateMachinePath 'class\s+NShinGetterSpriteAnimationStateMachine'
Require-Pattern 'State machine connects animation finished signal once' $stateMachinePath 'AnimationFinished[\s\S]*SignalConnected'
Require-Pattern 'State machine maps Attack trigger to attack animation' $stateMachinePath 'trigger\s+switch[\s\S]*"Attack"\s*=>\s*NShinGetterSpriteSequence\.AttackAnimationName'
Require-Pattern 'State machine maps Cast trigger to cast animation' $stateMachinePath 'trigger\s+switch[\s\S]*"Cast"\s*=>\s*NShinGetterSpriteSequence\.CastAnimationName'
Require-Pattern 'State machine returns to idle when one-shot animation finishes' $stateMachinePath 'OnAnimationFinished[\s\S]*PlayIdle'

Require-Pattern 'Static visuals exposes Getter One action animation helper' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'TryPlayGetterOneActionAnimation'
Require-Pattern 'Static visuals only plays action animation on visible Getter One' 'src\Nodes\Combat\NShinGetterStaticVisuals.cs' 'getterOneAnimation\.Visible[\s\S]*Modulate\.A'

$patchPath = 'src\Patches\ShinGetterCreatureAnimationPatch.cs'
Require-Pattern 'Creature animation patch hooks SetAnimationTrigger' $patchPath 'HarmonyPatch\(typeof\(NCreature\),\s*nameof\(NCreature\.SetAnimationTrigger\)\)'
Require-Pattern 'Creature animation patch limits to Shin Getter character' $patchPath 'Character\s+is\s+not\s+ShinGetter'
Require-Pattern 'Creature animation patch forwards Attack and Cast triggers' $patchPath 'trigger\s+is\s+not\s+\("Attack"\s+or\s+"Cast"\)'
Require-Pattern 'Creature animation patch plays Getter action animation' $patchPath 'TryPlayGetter(Action)?Animation'

Require-Pattern 'PCK validator checks Getter One attack frames' 'tools\validate-mod-resources.gd' 'getter_one_attack/sprite_000001\.png[\s\S]*getter_one_attack/sprite_000121\.png'
Require-Pattern 'PCK validator checks Getter One cast frames' 'tools\validate-mod-resources.gd' 'getter_one_cast/sprite_000001\.png[\s\S]*getter_one_cast/sprite_000121\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.13 / Getter One action animation checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.13 / Getter One action animation checks passed.'

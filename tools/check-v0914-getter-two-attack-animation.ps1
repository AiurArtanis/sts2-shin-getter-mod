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

Require-Pattern 'Root manifest version is v0.9.14+' 'ShinGetterMod.json' '"version":\s*"v0\.9\.(1[4-9]|[2-9]\d)"'

Require-FileCount 'Getter Two attack animation exports 40 png frames' 'images\characters\shin_getter\forms\getter_two_attack' '*.png' 40

Require-Pattern 'GetterTwo is AnimatedSprite2D in creature scene' 'scenes\creature_visuals\shin_getter.tscn' '\[node name="GetterTwo" type="AnimatedSprite2D"'

$sequencePath = 'src\Nodes\Combat\NShinGetterSpriteSequence.cs'
Require-Pattern 'Sprite sequence declares Getter Two attack frame directory' $sequencePath 'GetterTwoAttackFrameDirectory\s*=.*getter_two_attack'
Require-Pattern 'Sprite sequence declares Getter Two attack frame cap' $sequencePath 'GetterTwoAttackMaxFrames\s*=\s*40'
Require-Pattern 'Sprite sequence exposes Getter Two loader' $sequencePath 'EnsureGetterTwoLoaded'
Require-Pattern 'Sprite sequence prepares Getter Two idle animation' $sequencePath 'EnsureGetterTwoLoaded[\s\S]*IdleAnimationName'
Require-Pattern 'Sprite sequence loads Getter Two one-shot attack animation' $sequencePath 'EnsureGetterTwoLoaded[\s\S]*LoadLinearAnimation[\s\S]*GetterTwoAttackFrameDirectory[\s\S]*loop:\s*false'

$stateMachinePath = 'src\Nodes\Combat\NShinGetterSpriteAnimationStateMachine.cs'
Require-Pattern 'State machine accepts a form-specific loader' $stateMachinePath 'Action<AnimatedSprite2D>[\s\S]*ensureLoaded'
Require-Pattern 'State machine keeps default Getter One overload' $stateMachinePath 'TryPlay\(AnimatedSprite2D sprite,\s*string trigger\)[\s\S]*EnsureLoaded'
Require-Pattern 'State machine can return any form to idle' $stateMachinePath 'PlayIdle\(AnimatedSprite2D sprite,\s*Action<AnimatedSprite2D>'

$staticVisualsPath = 'src\Nodes\Combat\NShinGetterStaticVisuals.cs'
Require-Pattern 'Static visuals exposes generic Getter action animation helper' $staticVisualsPath 'TryPlayGetterActionAnimation'
Require-Pattern 'Static visuals can play visible Getter Two action animation' $staticVisualsPath 'Visuals/GetterTwo[\s\S]*EnsureGetterTwoLoaded'
Require-Pattern 'Static visuals initializes Getter Two animation frames during form lookup' $staticVisualsPath 'getterTwoAnimation[\s\S]*EnsureGetterTwoLoaded'

$patchPath = 'src\Patches\ShinGetterCreatureAnimationPatch.cs'
Require-Pattern 'Creature animation patch uses generic Getter action animation helper' $patchPath 'TryPlayGetterActionAnimation'

Require-Pattern 'PCK validator checks Getter Two attack frames' 'tools\validate-mod-resources.gd' 'getter_two_attack/sprite_000001\.png[\s\S]*getter_two_attack/sprite_000121\.png'

if ($failures.Count -gt 0) {
    Write-Host 'RED: v0.9.14 / Getter Two attack animation checks failing:'
    $failures | Select-Object -First 120
    exit 1
}

Write-Host 'GREEN: v0.9.14 / Getter Two attack animation checks passed.'

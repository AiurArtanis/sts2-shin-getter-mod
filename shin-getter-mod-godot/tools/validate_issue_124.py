#!/usr/bin/env python3
"""issue#124 / issue#215 source + delivered-data contracts, not runtime visual acceptance.

No game/Godot launch, PCK, audio playback or deployment. Production C# mask/data
execution is separately covered by tests/Issue124VoiceMask (managed-only).
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import wave

from validate_b130_core import block, compact, has, ordered, require

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent
CARD = "src/Models/Cards/SGC_StarSlash.cs"
SEQUENCE = "src/Nodes/Combat/NShinGetterStarSlashSequence.cs"
DATA = "src/Nodes/Combat/NShinGetterStarSlashData.cs"
VOICE = "src/Audio/ShinGetterVoiceService.cs"
FORMS = {"getter_one_star_slash": (76, 133), "shin_getter_dragon_star_slash": (71, 130)}
PREPARATION_AUDIO = {
    "ryoma_burn_shin_dragon.wav": "7c0b38adc354b32ed2cf46f65d765b351f5fd8434dedcd2533a188d6a2df19bb",
    "ryoma_go_shin_getter.wav": "a3fd52b8601f5267fff256fe93366ce10bf7f7123d1bbeb2a7d416b6cff6f8ed",
}


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def card_contract(source: str) -> None:
    play = block(source, "protected override async Task OnPlay(")
    for token in ("pile.Cards.Count > 0", "prefs.RequireManualConfirmation || pile.Cards.Count > prefs.MinSelect",
                  "CardSelectCmd.Selector == null", "!NonInteractiveMode.IsActive",
                  "RunManager.Instance.NetService.Type != NetGameType.Replay"):
        has(play, token, "Preparation must mirror real native UI eligibility")
    require("RequireManualConfirmation = true" not in source, "Do not force UI for native automatic selections")
    ordered(play, ["NShinGetterStarSlashSequence.TryCreate(Owner)", "try",
                   "if (willChoose) ShinGetterVoiceService.TryPlayStarSlashPreparation(this);",
                   "await CardSelectCmd.FromCombatPile", "ShinGetterVoiceService.FinishStarSlashPreparation(Owner);",
                   "TryPlayCardVoiceAtCustomTiming(this, out float voiceDuration)", "sequence?.Confirm(voiceDuration);",
                   "Math.Min(selected.Sum(SumOriginalCardValues), 50m)", "await CardCmd.Exhaust(choiceContext, card);",
                   "ShinGetterCombatVfx.FlashHotBloodIcon(Owner.Creature);",
                   "if (HasForm(Owner, ShinGetterForm.Getter1))", "await PowerCmd.Apply<SGP_HotBlood>",
                   "await sequence.PlayToImpact();", "await ShinGetterCombatVfx.PlayHeavyCleave",
                   "await DamageCmd.Attack", '.WithHitFx("vfx/vfx_giant_horizontal_slash")',
                   "await sequence.Recover();", "finally", "sequence?.Close();",
                   "ShinGetterVoiceService.FinishStarSlashPreparation(Owner);"], "Selection/confirmation/exhaust/impact/cleanup")
    before_ui = play[:play.index("await CardSelectCmd.FromCombatPile")]
    require("await sequence" not in before_ui, "Raise must run concurrently with native selector")
    require(play.count("DamageCmd.Attack") == 1 and play.count("PowerCmd.Apply<SGP_HotBlood>") == 1,
            "Single attack and single compatible Getter1 Valor application")
    for token in ("new DamageVar(22m, ValueProp.Move)", "new CardsVar(1)",
                  "base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)",
                  "DynamicVars.Cards.UpgradeValueBy(1m)", ".WithNoAttackerAnim()"):
        has(source, token, "Keep gameplay values and avoid generic animation restart")


def sequence_contract(source: str) -> None:
    create = block(source, "public static NShinGetterStarSlashSequence? TryCreate(")
    for token in ("NonInteractiveMode.IsActive", "CombatManager.Instance.IsOverOrEnding", "player.Creature.IsDead",
                  "FastModeType.Instant", "player.Creature.HasPower<SGP_ShinForm>()",
                  "player.Creature.HasPower<SGP_ShinGetterOne>()", "IsControlling(sprite)",
                  "NShinGetterShiningSparkSequence.IsControlling(sprite)", "if (data == null) return null;",
                  "sprite.SetMeta(OwnerMeta, sequence.GetInstanceId());", "sprite.AddChild(sequence);",
                  "sequence._prepared = sequence.Phase(0f, data.HoldTime);"):
        has(create, token, "Visible owner-specific creation/fallback boundary")
    require("await " not in create, "Do not delay native UI for the raise")
    impact = block(source, "public async Task PlayToImpact()")
    ordered(impact, ["await Task.WhenAny(_prepared, _ended.Task)", "if (_closed) return;", "await _prepared;",
                     "_voiceDuration - (_clock - _confirmedAt)", "0f, 2f", "await Hold(hold)",
                     "await Phase(_data.HoldTime, _data.ImpactTime)"], "Fast confirmation/remaining voice/bounded hold")
    phase = block(source, "private Task Phase(")
    has(phase, "if (_closed || !CanContinue())", "Every phase rechecks visual ownership before mutation")
    process = block(source, "public override void _Process(")
    ordered(process, ["!CanContinue()", "Close(); return;", "if (CombatManager.Instance.IsPaused) return;",
                      "_clock +=", "SetPose(time)", "_phaseDone.TrySetResult(true)"], "Pause/interrupt/resume clock")
    pose = block(source, "private void SetPose(")
    has(pose, "Math.Abs(time - _data.HoldTime) < 0.00001f ? _data.HoldFrame : _data.FrameAt(time)", "Hold uses the actual held frame, not next cleave")
    for token in ("_sprite.GetMeta(OwnerMeta).AsUInt64() == GetInstanceId()",
                  "NShinGetterShiningSparkSequence.IsActuallyVisible(_sprite)",
                  "_sprite.Animation == NShinGetterSpriteSequence.StarSlashAnimationName",
                  "public override void _ExitTree() => End();"):
        has(source, token, "Lost owner/visibility/action/tree must interrupt safely")
    end = block(source, "private void End()")
    ordered(end, ["if (_closed) return;", "_closed = true;", "FinishStarSlashPreparation(_player)",
                  "if (OwnsSprite())", "_sprite.RemoveMeta(OwnerMeta);", "!_player.Creature.IsDead",
                  "PlayIdle", "_phaseDone?.TrySetResult(false)", "_ended.TrySetResult(false)"], "Cleanup is scoped and releases waiters")
    draw = block(source, "public override void _Draw()")
    ordered(draw, ["NShinGetterStarSlashData.BuildWeapon(frame, _poseTime, _data.HoldTime)",
                   "frame.WeaponCover", "DrawColoredPolygon", "MergeOutline(opaque)", "frame.Hands", "DrawPolygon"],
            "Opaque weapon overlay then textured hand foreground")
    has(draw, "new(37f / 255f, 219f / 255f, 103f / 255f, 1f)", "Getter-line opaque green covers source weapon")
    has(draw, "frame.WeaponCover.Concat(geometry.Blades).Append(geometry.Shaft)", "Draw curved blade plus tapered shaft over old weapon")
    ordered(draw, ["DrawColoredPolygon(polygon, core)", "geometry.BladeLights", "MergeOutline(opaque)",
                   "frame.BodyForeground.Concat(frame.Hands)"], "Inside-blade energy layer does not obscure restored armor/hands")
    has(draw, "new Color(128f / 255f, 1f, 183f / 255f, 1f)", "Opaque inner Getter-line energy band, not a flat placeholder")
    require("24f" not in draw and "DrawLine(" not in draw, "Do not restore the rejected constant-width rectangular shaft")
    has(source, "Geometry2D.MergePolygons(outlines[left], outlines[right])", "One union edge preserves concavity, not old/new double heads")
    has(draw, "frame.BodyForeground.Concat(frame.Hands)", "Restore genuine armor occlusion first, fingers last")
    for token in ("texture is AtlasTexture atlas", "foregroundTexture = atlas.Atlas;", "uvOrigin = atlas.Region.Position;",
                  "Vector2 foregroundSize = foregroundTexture.GetSize();",
                  "NShinGetterStarSlashData.ForegroundUv(", "point, uvOrigin, foregroundSize"):
        has(draw, token, "Foreground samples the actual frame region of the atlas, not the whole sheet")
    require("ClipChildren" not in source and "SetDeferred" not in source and "CreateTween" not in source,
            "No 720px clip or unmanaged tween completion dependency")


def voice_contract(source: str) -> None:
    has(source, 'new("016", ShinGetterVoiceCue.StarSlash, "ryoma_star_slash.wav", "SHIN_GETTER.voice.starSlash", ShinGetterForm.Getter1)',
        "Confirmation voice keeps existing Getter1/Dragon compatibility and deferred timing")
    has(source, "bool StartAtCardPlay = false", "016 must not be claimed by card-play-start Prefix before selection")
    for token in ('new("066", ShinGetterVoiceCue.StarSlashDragonPreparation, "ryoma_burn_shin_dragon.wav"',
                  'new("067", ShinGetterVoiceCue.StarSlashOnePreparation, "ryoma_go_shin_getter.wav"',
                  "StarSlashDragonPreparation = 62", "StarSlashOnePreparation = 63"):
        has(source, token, "066/067 independent codes/audio/history bits")
    prep = block(source, "internal static void TryPlayStarSlashPreparation(")
    ordered(prep, ["card is not SGC_StarSlash", "player.Creature.HasPower<SGP_ShinForm>()",
                   "ShinGetterVoiceCue.StarSlashDragonPreparation", "player.Creature.HasPower<SGP_ShinGetterOne>()",
                   "ShinGetterVoiceCue.StarSlashOnePreparation", "TryPlayOneTime"], "Actual form priority, existing three-mode claim policy")
    require("HasForm(" not in prep, "Dragon compatibility must not misroute preparation to one")
    finish = block(source, "internal static void FinishStarSlashPreparation(")
    for token in ("PlaybackStates.GetOrCreateValue(player)", "state.StarSlashPreparationPlayer = null;",
                  "state.ActiveVoicePlayers.Remove(audio)", "audio.Stop();", "audio.QueueFree();",
                  "state.CurrentSubtitle == state.StarSlashPreparationSubtitle"):
        has(finish, token, "Stop only this owner's preparation, not another/newer voice")
    require("StopActiveVoiceAudio(" not in finish, "Preparation cleanup must not stop all voices")
    location = block(source, "private static (bool Low, int Bit) GetLocation(")
    for token in ("index is < 0 or >= 64", "62 => (true, int.MinValue)", "63 => (false, int.MinValue)",
                  "< 31 => (true, 1 << index)", "_ => (false, 1 << (index - 31))"):
        has(location, token, "Released 31+31 mapping preserved; only unused sign bits extend history")
    require(source.count("var (low, bit) = GetLocation(cue);") == 2, "Add/Contains share bounds and mapping")


def data_contract(source: str) -> None:
    for token in ("Frames.Length is not (76 or 71)", "frame.Duration <= 0f", "!float.IsFinite(frame.Duration)",
                  "!IsFinite(frame.Grip)", "!IsFinite(frame.Axis)", "frame.Grip.DistanceTo(frame.Axis) < 1f",
                  "frame.WeaponCover.Length == 0", "frame.BladeCover.Length == 0", "frame.HandleCover.Length == 0", "frame.Hands.Length == 0", "!float.IsFinite(ImpactTime)",
                  "ImpactTime <= HoldTime", "ImpactTime >= TotalTime", "points.Length < 3",
                  "if (!FileAccess.FileExists(path)) return null;", "return null;", "end += Frames[index].Duration;"):
        has(source, token, "Reject incomplete metadata; use weighted frame durations")
    geometry = block(source, "internal static WeaponGeometry BuildWeapon(")
    has(source, "(point + origin) / textureSize", "Frame-local pixels are mapped into the atlas region")
    for token in ("frame.BladeCover.SelectMany", "frame.HandleCover.SelectMany", "step <= 18", "step <= 8",
                  "Lobe(1f, 1f, -1f)", "Lobe(0.8f, 0.82f, 1f)", "t * t * (3f - 2f * t)"):
        has(geometry, token, "Shared reference-driven asymmetric Bezier blade / smoothstep tapered shaft")
    require("BuildBlade" not in source and "hull" not in geometry.lower(), "No rejected convex-hull placeholder")
    profile = json.loads(read("tools/star-slash-reference-geometry.json"))
    require(profile["job_id"] == "ART-004" and profile["blade"]["samples_per_cubic"] == 18,
            "Retained source geometry proposal matches this implementation")
    expected = [point for segment in profile["blade"]["left"]["cubic_segments"] for point in segment]
    require(expected == [point for segment in profile["blade"]["right"]["cubic_segments"] for point in segment],
            "Same interpreted curves with asymmetric length/width, not different unreviewed lobes")
    controls = geometry.split("Vector2[][] segments =", 1)[1].split("Vector2[] Lobe(", 1)[0]
    actual = [[float(x), float(y)] for x, y in re.findall(r"new Vector2\(([-0-9.]+)f, ([-0-9.]+)f\)", controls)] + [[0, 0]]
    require(actual == expected, "Actual production Bezier controls match the reference proposal exactly")
    band = block(source, "private static Vector2[] LightBand(")
    for token in ("innerCurve[segment].Dot(axis) < along", "Vector2 innerPoint = a.Lerp(b, t)",
                  "outerPoint.Lerp(innerPoint, 0.28f)", "outerPoint.Lerp(innerPoint, 0.72f)", "outer.Add(blade[36])"):
        has(band, token, "Inner energy follows the blade ribbon and preserves the concave opening")


def point(value) -> bool:
    return isinstance(value, list) and len(value) == 2 and all(isinstance(n, (int, float)) and math.isfinite(n) for n in value)


def simple_polygon(polygon) -> None:
    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    for left in range(len(polygon)):
        for right in range(left + 2, len(polygon)):
            if left == 0 and right == len(polygon) - 1:
                continue
            a, b = polygon[left], polygon[(left + 1) % len(polygon)]
            c, d = polygon[right], polygon[(right + 1) % len(polygon)]
            crossing = cross(a, b, c) * cross(a, b, d) < -0.0001 and cross(c, d, a) * cross(c, d, b) < -0.0001
            require(not crossing, "Coverage/foreground polygons must be simple for Godot triangulation")


def delivered_contract(document: dict, delivery: dict, count: int, hold_source: int) -> None:
    frames = document["frames"]
    require(len(frames) == count == len(delivery["frames"]), "Exact delivered frame count")
    require(document["impact_time"] == 1.4, "Impact event is 1.4s, not animation completion")
    require(0 <= document["hold_frame"] < count, "Valid hold index")
    require(frames[document["hold_frame"]]["source_frame"] == hold_source, "Hold source candidate exact")
    for index, (frame, original) in enumerate(zip(frames, delivery["frames"])):
        require(frame["index"] == index and all(frame[key] == original[key] for key in original),
                "Exact ART-003 mapping/timing/hash provenance")
        require(math.isfinite(frame["duration_seconds"]) and frame["duration_seconds"] > 0, "Finite positive duration")
        require(point(frame["grip"]) and point(frame["axis"]) and math.dist(frame["grip"], frame["axis"]) >= 1,
                "Finite distinct grip/shaft direction")
        for name in ("weapon_cover", "blade_cover", "handle_cover", "hands"):
            require(bool(frame[name]), "Weapon cover and hand foreground must exist for every frame")
            for polygon in frame[name]:
                require(len(polygon) >= 3 and all(point(p) for p in polygon), "Finite complete polygon")
                simple_polygon(polygon)
        for polygon in frame.get("body_foreground", []):
            require(len(polygon) >= 3 and all(point(p) for p in polygon), "Valid optional armor occlusion polygon")
            simple_polygon(polygon)
    total = sum(frame["duration_seconds"] for frame in frames)
    hold = sum(frame["duration_seconds"] for frame in frames[:document["hold_frame"] + 1])
    require(math.isclose(total, 2.4, abs_tol=1e-6) and hold < 1.4 < total, "Weighted 2.4s phase envelope")


def source_checks() -> int:
    sources = {CARD: read(CARD), SEQUENCE: read(SEQUENCE), DATA: read(DATA), VOICE: read(VOICE)}
    for path, gate in ((CARD, card_contract), (SEQUENCE, sequence_contract), (VOICE, voice_contract), (DATA, data_contract)):
        gate(sources[path])
    mutants = [
        (card_contract, CARD, "pile.Cards.Count > prefs.MinSelect", "pile.Cards.Count >= prefs.MinSelect"),
        (card_contract, CARD, "if (willChoose) ShinGetterVoiceService.TryPlayStarSlashPreparation(this);", "ShinGetterVoiceService.TryPlayStarSlashPreparation(this);"),
        (card_contract, CARD, "await CardSelectCmd.FromCombatPile", "CardSelectCmd.FromCombatPile"),
        (card_contract, CARD, "await sequence.PlayToImpact();", "_ = sequence.PlayToImpact();"),
        (card_contract, CARD, '.WithHitFx("vfx/vfx_giant_horizontal_slash")', '.WithHitFx("changed")'),
        (card_contract, CARD, "sequence?.Close();", "// removed close"),
        (sequence_contract, SEQUENCE, "await _prepared;", "// skip raise"),
        (sequence_contract, SEQUENCE, "0f, 2f", "0f, 200f"),
        (sequence_contract, SEQUENCE, "if (CombatManager.Instance.IsPaused) return;", "// paused clock continues"),
        (sequence_contract, SEQUENCE, "_phaseDone?.TrySetResult(false);", "// no phase release"),
        (sequence_contract, SEQUENCE, "? _data.HoldFrame", "? _data.HoldFrame + 1"),
        (sequence_contract, SEQUENCE, "_sprite.GetMeta(OwnerMeta).AsUInt64() == GetInstanceId()", "true"),
        (sequence_contract, SEQUENCE, "Geometry2D.MergePolygons(outlines[left], outlines[right])", "new Godot.Collections.Array<Godot.Vector2[]>()"),
        (sequence_contract, SEQUENCE, "uvOrigin = atlas.Region.Position;", "uvOrigin = Vector2.Zero;"),
        (data_contract, DATA, "(point + origin) / textureSize", "point / textureSize"),
        (data_contract, DATA, "frame.BladeCover.SelectMany", "frame.WeaponCover.SelectMany"),
        (data_contract, DATA, "frame.HandleCover.SelectMany", "frame.WeaponCover.SelectMany"),
        (data_contract, DATA, "step <= 18", "step <= 1"),
        (data_contract, DATA, "new Vector2(0.36f, 1.18f)", "new Vector2(0.36f, 0.3f)"),
        (data_contract, DATA, "outerPoint.Lerp(innerPoint, 0.28f)", "outerPoint.Lerp(innerPoint, -0.5f)"),
        (voice_contract, VOICE, "player.Creature.HasPower<SGP_ShinForm>()", "HasForm(player, ShinGetterForm.Getter1)"),
        (voice_contract, VOICE, "62 => (true, int.MinValue)", "62 => (false, 1 << 31)"),
        (voice_contract, VOICE, "state.CurrentSubtitle == state.StarSlashPreparationSubtitle", "true"),
        (voice_contract, VOICE, "bool StartAtCardPlay = false", "bool StartAtCardPlay = true"),
        (data_contract, DATA, "!IsFinite(frame.Grip)", "false"),
        (data_contract, DATA, "!float.IsFinite(ImpactTime)", "false"),
    ]
    for gate, path, before, after in mutants:
        require(before in sources[path], f"Mutation target exists: {before}")
        try:
            gate(sources[path].replace(before, after, 1))
        except AssertionError:
            continue
        raise AssertionError(f"Source mutant escaped: {before}")
    state = read("src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    guard = state.split("private static bool ShouldKeepActiveSpecialAnimation(", 1)[1].split("private static void PlayIdle(", 1)[0]
    for token in ("NShinGetterStarSlashSequence.IsControlling(sprite)", "StarSlashAnimationName"):
        has(guard, token, "Manual pause is protected from generic card hooks")
    for token in ('"Attack"', '"HeavyAttack"', '"Cast"', '"Dash"', '"Hit"'):
        has(guard, token, "Keep special action during exhaust/multihit reactions")
    require('"Dead"' not in guard and '"Death"' not in guard, "Death remains high-priority interruption")
    has(state, "state.NextActionSpeedScale = 1f;", "Suppressed ordinary follow-up consumes queued speed")
    sprites = read("src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    has(sprites, "data.Frames[index].Duration * framesPerSecond", "SpriteFrames use ART-003 variable weights, not count/30")
    card_base = read("src/Models/Cards/ShinGetterCardBase.cs")
    for name, following in (("MovementVfxTimingCards", "BlockAnimationCards"),
                            ("DeferredCardVoiceCards", "FormTransformCards")):
        section = card_base.split(f"IReadOnlySet<string> {name}", 1)[1].split(f"IReadOnlySet<string> {following}", 1)[0]
        has(section, '"SGC_StarSlash"', "No early base-class ordinary action/016 before selection")
    for action in FORMS:
        has(read("export_presets.cfg"), f"images/characters/shin_getter/forms/{action}/animation.json", "JSON explicitly exported")
        sidecar = read(f"images/characters/shin_getter/forms/{action}/sprite_sheet.png.import")
        has(sidecar, "compress/mode=0", "Lossless source RGB/alpha import")
    return len(mutants)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expect-red", metavar="BASELINE_REF")
    parser.add_argument("--source-only", action="store_true")
    parser.add_argument("--base-game-root", type=Path, default=Path("E:/Work/SlaytheSpare2"))
    args = parser.parse_args()
    if args.expect_red:
        baseline = subprocess.run(["git", "show", f"{args.expect_red}:shin-getter-mod-godot/{CARD}"], cwd=REPO,
                                  check=True, capture_output=True, text=True, encoding="utf-8").stdout
        try:
            card_contract(baseline)
        except AssertionError as error:
            print(f"EXPECTED RED {args.expect_red}: {error}")
            return
        raise AssertionError("Baseline unexpectedly implements issue#124")
    negatives = source_checks()
    if args.source_only:
        print(f"PASS source-only: {negatives} rejected mutants; delivered metadata NOT checked")
        return
    native_select = (args.base_game_root / "src/Core/Commands/CardSelectCmd.cs").read_text(encoding="utf-8-sig")
    native_prefs = (args.base_game_root / "src/Core/CardSelection/CardSelectorPrefs.cs").read_text(encoding="utf-8-sig")
    for token in ("if (num == 0)", "!prefs.RequireManualConfirmation && num <= prefs.MinSelect",
                  "if (Selector != null)", "NCombatPileCardSelectScreen.Create(pile, prefs, filter)",
                  "await nCombatPileCardSelectScreen.CardsSelected()"):
        has(native_select, token, "Read-only native selection evidence still matches eligibility")
    has(native_prefs, "RequireManualConfirmation = MinSelect >= 0 && MinSelect != MaxSelect;",
        "Fixed selection 1/2 keeps native automatic path")
    for filename, digest in PREPARATION_AUDIO.items():
        path = ROOT / "audio/sfx/characters/shin_getter/voices" / filename
        require(hashlib.sha256(path.read_bytes()).hexdigest() == digest, "066/067 are exact authorized source WAVs")
        with wave.open(str(path), "rb") as audio:
            require(audio.getnframes() > 0 and audio.getnchannels() in (1, 2), "Nonempty PCM preparation voice")
    for action, (count, hold_source) in FORMS.items():
        document = json.loads(read(f"images/characters/shin_getter/forms/{action}/animation.json"))
        delivery = json.loads((REPO / f"art_sources/characters/shin_getter/forms/{action}/delivery.json").read_text(encoding="utf-8"))
        delivered_contract(document, delivery, count, hold_source)
        for frame in document["frames"]:
            path = REPO / f"art_sources/characters/shin_getter/forms/{action}/sprite_{frame['index'] + 1:06d}.png"
            require(hashlib.sha256(path.read_bytes()).hexdigest() == frame["sha256"], "Exact PNG provenance")
        for mutation in (lambda d: d.update(hold_frame=-1), lambda d: d.update(impact_time=0.1),
                         lambda d: d["frames"][0].update(duration_seconds=0),
                         lambda d: d["frames"][0].update(grip=[math.inf, 0]),
                         lambda d: d["frames"][0].update(weapon_cover=[]),
                         lambda d: d["frames"][0].update(blade_cover=[]),
                         lambda d: d["frames"][0].update(handle_cover=[]),
                         lambda d: d["frames"][0].update(hands=[]),
                         lambda d: d["frames"][0].update(weapon_cover=[[[0, 0], [10, 10], [0, 10], [10, 0]]]),
                         lambda d: d["frames"][0].update(body_foreground=[[[0, 0], [10, 10], [0, 10], [10, 0]]])):
            variant = json.loads(json.dumps(document))
            mutation(variant)
            try:
                delivered_contract(variant, delivery, count, hold_source)
            except AssertionError:
                negatives += 1
                continue
            raise AssertionError("Delivered-data mutant escaped")
    print(f"issue#124 / issue#215 PASS: 147 mapped frames, weighted timing, scoped cleanup, {negatives} rejected variants.")
    print("Visual overlay, input UI and playback remain Artanis acceptance; no game/PCK/deployment executed.")


if __name__ == "__main__":
    main()

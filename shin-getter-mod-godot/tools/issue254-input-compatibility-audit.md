# issue#254: controller input compatibility

Parent: issue#93. Formal source: main@53012aee53083d61780e4fd1bd5ad4515f353a60, v1.3.0.
This delivery fixes the confirmed cross-version runtime input problem, not all
graphics stalls or the historical D3D12 device-removal fault.

## Source/API audit

- Formal109 NInputManager.Init overlays saved values without InputMap validation;
  _UnhandledInput invokes IsActionPressed/Released for every pair on every event.
- Beta111 declares nine renamed physical controller actions; formal109 does not.
  Beta also has MergeSavedControllerBindings validation, absent from formal109.
- Harmony targets: NInputManager._UnhandledInput(InputEvent),
  GetHotkeyIcon(string), ModifyControllerButton(StringName, StringName).
  Actual _controllerInputMap FieldRef and ControllerManager default bindings are
  checked against the read-only official source and compilation reference.
- Compatibility is based on current InputMap declarations, not hard-coded Beta
  aliases. Valid custom pairs remain unchanged; invalid keys/values use current
  legal defaults, or disappear only from the runtime copy when no default exists.

## Minimal implementation

- Copy-on-repair policy never mutates its source or saved settings.
- Consumer guard covers input and hotkey icon lookups, including initialization
  that occurred before mod patch installation.
- CWT tracks the current dictionary identity; warm input has no repeated scan.
  Native rebinding invalidates before its InputRebound signal. Replaced controller
  dictionaries automatically revalidate. No SettingsSave, save/reset or fake
  InputMap action is used. No keyboard map is changed.

## Verification

- 14 linked production policy/Harmony FieldRef assertions PASS; seven source
  regressions rejected. Formal109 build 0 warning/0 error.
- Rendered D3D12 same scoped diagnostic sequence, with private copied settings:
  baseline-old has 2592 missing-action errors, roughly 110ms input consumption;
  baseline-legal has zero such errors, input below 0.05ms;
  candidate-old also has zero, input below 0.05ms, normal p95 about 8.4ms.
- Candidate reports 11 runtime repairs: nine invalid physical values and two
  obsolete logical keys. Saved controller_mapping still matches the original;
  original settings file SHA-256 is unchanged. Valid custom-swap and rebinding/
  dictionary-replacement behavior is covered by linked-source fixtures.
- The visible graphics-backed diagnostic uses native input consumption,
  unblockable diagnostic damage and native form transformation APIs. It is not
  physical controller or normal paid-card acceptance and does not count toward
  any full-run/75-room regression. All instances ran serially in private profiles.

## Separate graphics findings and limits

- With legal settings or this repair, seconds-long cold spikes remain. Modded
  D3D12 start/first hit: about 2.14/2.16s; no-ShinGetterMod Ironclad control first
  hit: 2.77s. Vulkan Forward+ and OpenGL controls still have cold spikes, so merely
  changing renderer is not a product fix.
- Canvas pipeline count rises from 14 to 31 during modded battle entry and to 32
  at first hit. Native damage-number/hit-spark _Ready each takes about 0.5ms.
- Controlled extra native hit-spark first draw takes 1.65s; then the same first
  character hit takes 83ms with about 60ms cold sprite load. This isolates a
  common native effect/render-pipeline cold cost, rather than proving sprite LRU
  or the archived animation work is the seconds-long cause. No product warm-up,
  effect deletion, cache expansion or renderer configuration change is included.
- Historical 0x887a0005/data_ptr fault predates the candidate and started before
  combat. It did not recur in the bounded rendered cases; no matching recent
  Display/nvlddmkm event was returned. Its exact cause is still unconfirmed.
- No PR/merge/PCK generation/shared deployment/release, no game-source or player
  save modifications. Main development receives the private raw traces and
  rendered images for independent review. Human acceptance remains open.

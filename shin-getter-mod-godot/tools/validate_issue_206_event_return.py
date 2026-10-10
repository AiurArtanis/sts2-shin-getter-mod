#!/usr/bin/env python3
"""Static return-state gate, never invokes Godot/game or native input."""
from pathlib import Path
import argparse

ROOT = Path(__file__).resolve().parents[1]


def check():
    text = (ROOT / "src/Patches/ShinGetterBondDialoguePatch.cs").read_text(encoding="utf-8-sig")
    attach = text[text.index("internal static void Attach("):text.index("// The old localization")]
    assert "layout.OptionButtons.Where(button => button.IsEnabled).ToArray()" in attach, "Capture enabled-state ownership before disabling"
    assert attach.index("var enabledOptions") < attach.index("layout.DisableEventOptions()")
    assert attach.index("state.Returned = true") < attach.index("button.Enable();") < attach.index("resume();", attach.index("state.Returned = true"))
    for token in ("GodotObject.IsInstanceValid(button)", "button.IsInsideTree()", "!button.IsQueuedForDeletion()", "layout.OptionButtons.Contains(button)"):
        assert token in attach, token
    assert "if (state.Returned) { resume(); return; }" in attach
    assert "if (state.Returned || !GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree()) return;" in attach
    assert "if (layout.IsQueuedForDeletion()) return;" in attach
    assert "ShinGetterBondSession.IsEligible(model)" in text
    assert "!ShinGetterBondDialogueBridge.IsBlocking" in text
    for forbidden in ("ForceClick(", "OptionButtonClicked(", "ChooseLocalOption(", "Option.IsLocked =", "SetLocalPlayerReady"):
        assert forbidden not in attach, forbidden
    print("issue#206 event return structural PASS (not native input/reward acceptance)")


def official(root):
    option = (root / "src/Core/Nodes/Events/NEventOptionButton.cs").read_text(encoding="utf-8-sig")
    clickable = (root / "src/Core/Nodes/GodotExtensions/NClickableControl.cs").read_text(encoding="utf-8-sig")
    layout = (root / "src/Core/Nodes/Events/NEventLayout.cs").read_text(encoding="utf-8-sig")
    enable = option[option.index("public void EnableButton()"):option.index("protected override void OnRelease()")]
    assert "MouseFilter = MouseFilterEnum.Stop" in enable and "Enable();" not in enable
    assert "if (Option.IsLocked)" in option
    assert "if (_isEnabled && IsVisibleInTree() && IsFocused" in clickable
    force = clickable[clickable.index("public void ForceClick()"):clickable.index("public void SetEnabled(")]
    assert "OnRelease();" in force and "_isEnabled" not in force
    assert "public bool IsEnabled => _isEnabled;" in clickable and "_isEnabled = true;" in clickable
    assert "optionButton.Disable();" in layout[layout.index("public void DisableEventOptions()"):]
    print("official109 input/EnableButton boundary source probes PASS (not runtime)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path)
    args = parser.parse_args()
    check()
    if args.source_root:
        official(args.source_root)

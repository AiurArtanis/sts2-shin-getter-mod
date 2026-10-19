"""Compile two real formal109 method bodies into the CPU environment bindings.

Does not redistribute game source. The only syntax adaptation is a decompiler's
synthetic single-element collection to an array. No Godot or game is started.
"""
import hashlib
from pathlib import Path
import sys

source_root, output = map(Path, sys.argv[1:])


def method(path, signature):
    text = (source_root / path).read_text(encoding="utf-8-sig")
    start = text.index(signature)
    opening = text.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    body = text[start:end]
    print(f"Native source body SHA256 {path}: {hashlib.sha256(body.encode()).hexdigest()}")
    return body


state = method("src/Core/Models/EventModel.cs", "protected virtual void SetEventState(")
options = method("src/Core/Nodes/Rooms/NEventRoom.cs", "private void SetOptions(")
synthetic = 'new global::_003C_003Ez__ReadOnlySingleElementList<EventOption>(new EventOption(eventModel, Proceed, "PROCEED", false, true))'
assert options.count(synthetic) == 1, "Re-audit changed decompiler collection syntax"
options = options.replace(synthetic, 'new[] { new EventOption(eventModel, Proceed, "PROCEED", false, true) }')
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text("""// Generated from read-only native source. Never commit this file.
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
namespace MegaCrit.Sts2.Core.Models { public partial class EventModel {
""" + state + "\n} }\nnamespace MegaCrit.Sts2.Core.Nodes.Rooms { public sealed partial class NEventRoom {\n" + options + "\n} }\n", encoding="utf-8")

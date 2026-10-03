"""Static gate for event icons remaining below overlay screens."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
patch = (root / "src/Patches/ShinGetterEventInvasionPatch.cs").read_text(encoding="utf-8")
icon = patch.split('icon.Name = "ShinGetterOptionIcon";', 1)[1].split("__instance.AddChild(icon);", 1)[0]
z = re.search(r"icon.ZIndex = (\d+);", icon)
assert z and int(z.group(1)) == 0, "event icon must use the button's canvas ordering, below later overlays"
assert "icon.MouseFilter = Control.MouseFilterEnum.Ignore;" in icon
assert "label.OffsetRight = -reservedWidth;" in patch, "keep the existing text/icon spacing"
print("issue#239 static regression: PASS")

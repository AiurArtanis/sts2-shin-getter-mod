from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def _package_root() -> Path:
    return Path(__file__).resolve().parents[1] / "python" / "agent-harness"


sys.path.insert(0, str(_package_root()))

from cli_anything.slaythespare2_111_beta.core.release_scan import scan_production_targets  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description="Reject TEST-ONLY bridge signatures in production artifacts")
    parser.add_argument("targets", nargs="+", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = scan_production_targets(args.targets)
    text = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8", newline="\n")
    print(json.dumps(result, ensure_ascii=True, sort_keys=True))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

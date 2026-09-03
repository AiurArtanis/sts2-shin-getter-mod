from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
from dataclasses import dataclass
from pathlib import Path


EXPECTED_COUNT = 14
EXPECTED_AGGREGATE = "e6452ab14b7978719a039c2e340188628431fb7ea20c0ac7e61c4fb91b64b91d"
DEFAULT_SOURCE = Path(r"E:\Work\SlaytheSpare2-111-beta\agent-harness")
EXCLUDED_DIRS = {"__pycache__", ".pytest_cache", ".state"}


@dataclass(frozen=True)
class Entry:
    relative: str
    sha256: str


def _is_reparse(path: Path) -> bool:
    stat = path.lstat()
    return path.is_symlink() or bool(getattr(stat, "st_file_attributes", 0) & 0x400)


def collect(root: Path) -> tuple[list[Entry], str]:
    root = root.resolve(strict=True)
    entries: list[Entry] = []
    for current, dirs, files in os.walk(root, followlinks=False):
        current_path = Path(current)
        if _is_reparse(current_path):
            raise RuntimeError(f"reparse point rejected: {current_path}")
        dirs[:] = sorted(
            name
            for name in dirs
            if name not in EXCLUDED_DIRS and not name.endswith(".egg-info")
        )
        for name in sorted(files):
            path = current_path / name
            if path.suffix == ".pyc" or _is_reparse(path):
                if _is_reparse(path):
                    raise RuntimeError(f"reparse point rejected: {path}")
                continue
            relative = path.relative_to(root).as_posix()
            entries.append(Entry(relative, hashlib.sha256(path.read_bytes()).hexdigest()))
    entries.sort(key=lambda item: item.relative.encode("utf-8"))
    canonical = b"".join(
        item.relative.encode("utf-8") + b"\0" + item.sha256.encode("ascii") + b"\n"
        for item in entries
    )
    return entries, hashlib.sha256(canonical).hexdigest()


def verify_source(source: Path) -> list[Entry]:
    entries, aggregate = collect(source)
    if len(entries) != EXPECTED_COUNT or aggregate != EXPECTED_AGGREGATE:
        raise RuntimeError(
            f"baseline mismatch: count={len(entries)} aggregate={aggregate}; "
            f"expected count={EXPECTED_COUNT} aggregate={EXPECTED_AGGREGATE}"
        )
    return entries


def import_to(source: Path, destination: Path, entries: list[Entry]) -> None:
    destination = destination.resolve()
    if destination.exists() and any(destination.iterdir()):
        raise RuntimeError(f"destination must be absent or empty: {destination}")
    destination.mkdir(parents=True, exist_ok=True)
    for entry in entries:
        source_path = source / Path(entry.relative)
        target_path = destination / Path(entry.relative)
        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, target_path)
    copied, aggregate = collect(destination)
    if copied != entries or aggregate != EXPECTED_AGGREGATE:
        raise RuntimeError("post-copy baseline verification failed")


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify and import the frozen 0.1.0 harness baseline")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--destination", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    source = args.source.resolve(strict=True)
    entries = verify_source(source)
    if args.destination is not None:
        import_to(source, args.destination, entries)
    result = {
        "source": source.as_posix(),
        "destination": args.destination.resolve().as_posix() if args.destination else None,
        "file_count": len(entries),
        "aggregate_sha256": EXPECTED_AGGREGATE,
        "copied": args.destination is not None,
    }
    print(json.dumps(result, sort_keys=True) if args.json else result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

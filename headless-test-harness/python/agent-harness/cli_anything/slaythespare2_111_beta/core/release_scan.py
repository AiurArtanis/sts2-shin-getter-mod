from __future__ import annotations

import hashlib
import os
import zipfile
from pathlib import Path
from typing import Any, BinaryIO, Iterable, Sequence


DEFAULT_FORBIDDEN_SIGNATURES = (
    "Sts2HeadlessTestBridge",
    "STS2_TEST_ENABLE",
    "STS2_TEST_TOKEN",
    "sts2-test/v1",
    "E_OBSERVER_OVERFLOW",
    "component_test_host",
)

SKIP_DIRECTORIES = {".git", ".godot", "bin", "obj", "headless-test-harness", "package-out"}


def _patterns(signatures: Sequence[str]) -> list[tuple[str, str, bytes]]:
    result: list[tuple[str, str, bytes]] = []
    for signature in signatures:
        result.append((signature, "utf-8", signature.encode("utf-8")))
        result.append((signature, "utf-16le", signature.encode("utf-16le")))
    return result


def _scan_stream(
    stream: BinaryIO,
    patterns: Sequence[tuple[str, str, bytes]],
    *,
    target: str,
    location: str,
    entry: str | None,
    chunk_size: int,
) -> tuple[list[dict[str, Any]], str, int]:
    digest = hashlib.sha256()
    hits: list[dict[str, Any]] = []
    maximum = max((len(pattern) for _, _, pattern in patterns), default=1)
    carry = b""
    total = 0
    reported: set[tuple[str, str, int]] = set()
    while True:
        chunk = stream.read(chunk_size)
        if not chunk:
            break
        digest.update(chunk)
        combined = carry + chunk
        base_offset = total - len(carry)
        for signature, encoding, pattern in patterns:
            start = 0
            while True:
                index = combined.find(pattern, start)
                if index < 0:
                    break
                absolute = base_offset + index
                key = (signature, encoding, absolute)
                if key not in reported:
                    reported.add(key)
                    hits.append(
                        {
                            "target": target,
                            "location": location,
                            "entry": entry,
                            "signature": signature,
                            "encoding": encoding,
                            "offset": absolute,
                        }
                    )
                start = index + 1
        total += len(chunk)
        carry = combined[-(maximum - 1) :] if maximum > 1 else b""
    return hits, digest.hexdigest(), total


def _scan_file(
    path: Path,
    patterns: Sequence[tuple[str, str, bytes]],
    *,
    chunk_size: int,
) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]]]:
    target = str(path.resolve())
    with path.open("rb") as handle:
        raw_hits, raw_hash, raw_size = _scan_stream(
            handle,
            patterns,
            target=target,
            location="file_content",
            entry=None,
            chunk_size=chunk_size,
        )
    hits = raw_hits
    errors: list[dict[str, Any]] = []
    entries_scanned = 0
    if path.suffix.lower() == ".zip":
        try:
            with zipfile.ZipFile(path) as archive:
                for info in archive.infolist():
                    if info.is_dir():
                        continue
                    entries_scanned += 1
                    for signature, encoding, pattern in patterns:
                        encoded_name = info.filename.encode("utf-8")
                        if pattern in encoded_name:
                            hits.append(
                                {
                                    "target": target,
                                    "location": "entry_name",
                                    "entry": info.filename,
                                    "signature": signature,
                                    "encoding": encoding,
                                    "offset": encoded_name.find(pattern),
                                }
                            )
                    with archive.open(info) as entry_stream:
                        entry_hits, _, _ = _scan_stream(
                            entry_stream,
                            patterns,
                            target=target,
                            location="entry_content",
                            entry=info.filename,
                            chunk_size=chunk_size,
                        )
                    hits.extend(entry_hits)
        except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
            errors.append({"target": target, "code": "E_INVALID_ARGUMENT", "message": str(exc)})
    record = {
        "path": target,
        "kind": "zip" if path.suffix.lower() == ".zip" else "file",
        "bytes": raw_size,
        "sha256": raw_hash,
        "entries_scanned": entries_scanned,
    }
    return record, hits, errors


def _iter_directory(root: Path) -> Iterable[Path]:
    for current, directories, files in os.walk(root):
        directories[:] = sorted(name for name in directories if name not in SKIP_DIRECTORIES)
        current_path = Path(current)
        for name in sorted(files):
            yield current_path / name


def scan_production_targets(
    targets: Sequence[Path],
    *,
    signatures: Sequence[str] = DEFAULT_FORBIDDEN_SIGNATURES,
    chunk_size: int = 1024 * 1024,
) -> dict[str, Any]:
    if chunk_size < 16:
        raise ValueError("chunk_size must be at least 16 bytes")
    patterns = _patterns(signatures)
    records: list[dict[str, Any]] = []
    hits: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []
    for target in targets:
        path = target.expanduser()
        if not path.exists():
            errors.append({"target": str(path), "code": "E_NOT_FOUND", "message": "scan target does not exist"})
            continue
        if path.is_symlink():
            errors.append({"target": str(path), "code": "E_ISOLATION_BREACH", "message": "symlink target rejected"})
            continue
        if path.is_dir():
            file_records: list[dict[str, Any]] = []
            for child in _iter_directory(path):
                record, child_hits, child_errors = _scan_file(child, patterns, chunk_size=chunk_size)
                file_records.append(record)
                hits.extend(child_hits)
                errors.extend(child_errors)
            aggregate = hashlib.sha256()
            for record in sorted(file_records, key=lambda item: str(item["path"]).encode("utf-8")):
                relative = Path(record["path"]).relative_to(path.resolve()).as_posix()
                aggregate.update(relative.encode("utf-8") + b"\0" + record["sha256"].encode("ascii") + b"\n")
            records.append(
                {
                    "path": str(path.resolve()),
                    "kind": "directory",
                    "files_scanned": len(file_records),
                    "aggregate_sha256": aggregate.hexdigest(),
                }
            )
        elif path.is_file():
            record, child_hits, child_errors = _scan_file(path, patterns, chunk_size=chunk_size)
            records.append(record)
            hits.extend(child_hits)
            errors.extend(child_errors)
        else:
            errors.append({"target": str(path), "code": "E_INVALID_ARGUMENT", "message": "unsupported target type"})
    return {
        "schema": "sts2-production-reverse-scan/v1",
        "ok": not hits and not errors,
        "signatures": list(signatures),
        "targets": records,
        "hits": hits,
        "errors": errors,
    }

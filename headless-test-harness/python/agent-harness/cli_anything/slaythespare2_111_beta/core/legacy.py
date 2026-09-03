from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


SKIP_DIRS = {
    ".codegraph",
    ".git",
    ".godot",
    ".idea",
    ".state",
    "__pycache__",
    "agent-harness",
    "bin",
    "obj",
}

COMMAND_LINE_PATTERN = re.compile(
    r"CommandLineHelper\.(?:HasArg|GetValue|TryGetValue)\(\s*\"([^\"]+)\""
)
CONSOLE_VALUE_PATTERNS = {
    "name": re.compile(r"CmdName\s*=>\s*\"([^\"]*)\""),
    "args": re.compile(r"Args\s*=>\s*\"([^\"]*)\""),
    "description": re.compile(r"Description\s*=>\s*\"([^\"]*)\""),
    "networked": re.compile(r"IsNetworked\s*=>\s*(true|false)"),
}


class HarnessError(RuntimeError):
    """A user-facing harness failure."""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def find_project_root(explicit: str | Path | None = None) -> Path:
    candidates: list[Path] = []
    if explicit:
        candidates.append(Path(explicit).expanduser())
    env_root = os.environ.get("SLAYTHESPARE2_111_BETA_ROOT")
    if env_root:
        candidates.append(Path(env_root).expanduser())
    candidates.extend([Path.cwd(), *Path.cwd().parents])
    candidates.extend(Path(__file__).resolve().parents)

    seen: set[Path] = set()
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        if (resolved / "project.godot").is_file() and (resolved / "sts2.csproj").is_file():
            return resolved
    requested = f" at {explicit}" if explicit else ""
    raise HarnessError(f"Could not locate project.godot and sts2.csproj{requested}")


def iter_project_files(root: Path) -> Iterable[Path]:
    for current, dirs, files in os.walk(root):
        dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS)
        current_path = Path(current)
        for name in sorted(files):
            yield current_path / name


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def project_status(root: Path) -> dict[str, Any]:
    project_text = _read_text(root / "project.godot")
    csproj_text = _read_text(root / "sts2.csproj")
    name_match = re.search(r'^config/name="([^"]+)"', project_text, re.MULTILINE)
    scene_match = re.search(r'^run/main_scene="([^"]+)"', project_text, re.MULTILINE)
    framework_match = re.search(r"<TargetFramework>([^<]+)</TargetFramework>", csproj_text)
    sdk_match = re.search(r'<Project\s+Sdk="([^"]+)"', csproj_text)

    counts = {
        "csharp": 0,
        "scenes": 0,
        "resources": 0,
        "localization_json": 0,
        "console_commands": 0,
    }
    total = 0
    for path in iter_project_files(root):
        total += 1
        suffix = path.suffix.lower()
        if suffix == ".cs":
            counts["csharp"] += 1
            if path.name.endswith("ConsoleCmd.cs"):
                counts["console_commands"] += 1
        elif suffix == ".tscn":
            counts["scenes"] += 1
        elif suffix == ".tres":
            counts["resources"] += 1
        elif suffix == ".json" and "localization" in {part.lower() for part in path.parts}:
            counts["localization_json"] += 1

    return {
        "project_root": str(root),
        "project_name": name_match.group(1) if name_match else None,
        "main_scene": scene_match.group(1) if scene_match else None,
        "dotnet_sdk": sdk_match.group(1) if sdk_match else None,
        "target_framework": framework_match.group(1) if framework_match else None,
        "codegraph_index": str(root / ".codegraph") if (root / ".codegraph").is_dir() else None,
        "total_files": total,
        "counts": counts,
    }


def list_project_files(root: Path, pattern: str, limit: int) -> dict[str, Any]:
    matches: list[str] = []
    total = 0
    for path in iter_project_files(root):
        relative = path.relative_to(root)
        if relative.match(pattern) or path.match(pattern):
            total += 1
            if len(matches) < limit:
                matches.append(relative.as_posix())
    return {"pattern": pattern, "total": total, "returned": len(matches), "files": matches}


def search_source(root: Path, query: str, glob: str, limit: int) -> dict[str, Any]:
    rg = shutil.which("rg")
    if not rg:
        raise HarnessError("rg is required for source search")
    command = [rg, "--line-number", "--no-heading", "--color", "never", "--glob", glob, query, str(root)]
    result = run_process(command, cwd=root, timeout=60)
    if result["returncode"] not in (0, 1):
        raise HarnessError(result["stderr"] or "rg failed")
    lines = [line for line in result["stdout"].splitlines() if line]
    return {
        "query": query,
        "glob": glob,
        "total_matches": len(lines),
        "returned": min(len(lines), limit),
        "matches": lines[:limit],
    }


def discover_command_line_args(root: Path) -> list[dict[str, Any]]:
    occurrences: dict[str, list[str]] = {}
    for path in iter_project_files(root):
        if path.suffix.lower() != ".cs":
            continue
        for line_number, line in enumerate(_read_text(path).splitlines(), start=1):
            for match in COMMAND_LINE_PATTERN.finditer(line):
                occurrences.setdefault(match.group(1), []).append(
                    f"{path.relative_to(root).as_posix()}:{line_number}"
                )
    return [
        {"argument": argument, "occurrences": locations}
        for argument, locations in sorted(occurrences.items())
    ]


def _unescape_csharp_string(value: str | None) -> str | None:
    if value is None:
        return None
    return value.replace(r"\n", "\n").replace(r'\"', '"').replace(r"\\", "\\")


def discover_console_commands(root: Path) -> list[dict[str, Any]]:
    commands: list[dict[str, Any]] = []
    console_root = root / "src" / "Core" / "DevConsole" / "ConsoleCommands"
    if not console_root.is_dir():
        return commands
    for path in sorted(console_root.rglob("*ConsoleCmd.cs")):
        text = _read_text(path)
        class_match = re.search(r"public\s+(?:sealed\s+)?class\s+(\w+ConsoleCmd)\b", text)
        values = {key: pattern.search(text) for key, pattern in CONSOLE_VALUE_PATTERNS.items()}
        command_name = values["name"].group(1) if values["name"] else None
        if not command_name:
            continue
        commands.append(
            {
                "name": _unescape_csharp_string(command_name),
                "class": class_match.group(1) if class_match else path.stem,
                "args": _unescape_csharp_string(values["args"].group(1)) if values["args"] else None,
                "description": _unescape_csharp_string(values["description"].group(1)) if values["description"] else None,
                "networked": values["networked"].group(1) == "true" if values["networked"] else None,
                "source": path.relative_to(root).as_posix(),
            }
        )
    return sorted(commands, key=lambda item: str(item["name"]))


def strip_ansi(value: str) -> str:
    return re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", value)


def run_process(
    command: Sequence[str],
    *,
    cwd: Path,
    timeout: int = 300,
    env: dict[str, str] | None = None,
) -> dict[str, Any]:
    started = time.monotonic()
    try:
        completed = subprocess.run(
            [str(part) for part in command],
            cwd=str(cwd),
            env=env,
            text=True,
            encoding="utf-8",
            errors="replace",
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=timeout,
            check=False,
        )
    except subprocess.TimeoutExpired as exc:
        raise HarnessError(f"Command timed out after {timeout}s: {' '.join(command)}") from exc
    return {
        "command": [str(part) for part in command],
        "cwd": str(cwd),
        "returncode": completed.returncode,
        "duration_seconds": round(time.monotonic() - started, 3),
        "stdout": strip_ansi(completed.stdout),
        "stderr": strip_ansi(completed.stderr),
    }


def resolve_tool(name: str) -> str:
    resolved = shutil.which(name)
    if not resolved:
        raise HarnessError(f"Required executable is not on PATH: {name}")
    return resolved


def codegraph_command(operation: str, values: Sequence[str] = ()) -> list[str]:
    return [resolve_tool("codegraph"), operation, *values]


def run_codegraph(
    root: Path,
    operation: str,
    values: Sequence[str] = (),
    *,
    timeout: int = 180,
    max_chars: int = 20000,
) -> dict[str, Any]:
    result = run_process(codegraph_command(operation, values), cwd=root, timeout=timeout)
    result["stdout_truncated"] = len(result["stdout"]) > max_chars
    result["stdout"] = result["stdout"][:max_chars]
    result["stderr"] = result["stderr"][:max_chars]
    return result


def dotnet_build_command(root: Path, configuration: str, no_restore: bool) -> list[str]:
    command = [
        resolve_tool("dotnet"),
        "build",
        str(root / "sts2.csproj"),
        "--nologo",
        "--verbosity",
        "minimal",
        "--configuration",
        configuration,
    ]
    if no_restore:
        command.append("--no-restore")
    return command


def dotnet_restore_command(root: Path) -> list[str]:
    return [resolve_tool("dotnet"), "restore", str(root / "sts2.csproj"), "--nologo"]


def discover_godot(root: Path, configured: str | None = None) -> list[str]:
    candidates: list[Path] = []
    if configured:
        candidates.append(Path(configured).expanduser())
    if os.environ.get("GODOT"):
        candidates.append(Path(os.environ["GODOT"]).expanduser())
    for name in (
        "godot",
        "godot4",
        "Godot_v4.5.1-stable_mono_win64_console.exe",
        "Godot_v4.5.1-stable_mono_win64.exe",
    ):
        found = shutil.which(name)
        if found:
            candidates.append(Path(found))

    godot_root = root.parent / "Godot"
    if godot_root.is_dir():
        candidates.extend(sorted(godot_root.glob("**/Godot*_console.exe")))
        candidates.extend(sorted(godot_root.glob("**/Godot*.exe")))

    unique: list[str] = []
    seen: set[str] = set()
    for candidate in candidates:
        resolved = candidate.resolve()
        key = str(resolved).lower()
        if key in seen or not resolved.is_file() or "godotpcktool" in key:
            continue
        seen.add(key)
        unique.append(str(resolved))
    return unique


def godot_command(
    executable: str,
    root: Path,
    mode: str,
    *,
    quit_after: int = 120,
    editor: bool = False,
    headless: bool = False,
    extra_args: Sequence[str] = (),
) -> list[str]:
    command = [executable, "--path", str(root)]
    if mode == "import":
        command.extend(["--headless", "--import"])
    elif mode == "smoke":
        command.extend(["--headless", "--quit-after", str(quit_after)])
    elif mode == "launch":
        if headless:
            command.append("--headless")
        if editor:
            command.append("--editor")
        command.extend(extra_args)
    else:
        raise HarnessError(f"Unknown Godot mode: {mode}")
    return command


@dataclass
class FileLock:
    path: Path
    timeout: float = 10.0
    _handle: Any = None

    def __enter__(self) -> "FileLock":
        self.path.parent.mkdir(parents=True, exist_ok=True)
        deadline = time.monotonic() + self.timeout
        try:
            with self.path.open("xb") as seed:
                seed.write(b"0")
                seed.flush()
                os.fsync(seed.fileno())
        except FileExistsError:
            # Another process/thread owns initialization. Wait until its one-byte
            # lock range is durable before opening the shared lock file.
            while self.path.stat().st_size == 0:
                if time.monotonic() >= deadline:
                    raise HarnessError(f"Timed out initializing state lock: {self.path}")
                time.sleep(0.005)
        self._handle = self.path.open("r+b")
        while True:
            try:
                self._handle.seek(0)
                if os.name == "nt":
                    import msvcrt

                    msvcrt.locking(self._handle.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl

                    fcntl.flock(self._handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                return self
            except OSError:
                if time.monotonic() >= deadline:
                    self._handle.close()
                    raise HarnessError(f"Timed out waiting for state lock: {self.path}")
                time.sleep(0.05)

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        if not self._handle:
            return
        self._handle.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(self._handle.fileno(), msvcrt.LK_UNLCK, 1)
        else:
            import fcntl

            fcntl.flock(self._handle.fileno(), fcntl.LOCK_UN)
        self._handle.close()


class SessionStore:
    def __init__(self, state_dir: Path):
        self.state_dir = state_dir
        self.state_file = state_dir / "session.json"
        self.lock_file = state_dir / "session.lock"

    @staticmethod
    def default_state() -> dict[str, Any]:
        return {"version": 1, "config": {}, "history": [], "future": [], "updated_at": None}

    def load(self) -> dict[str, Any]:
        if not self.state_file.is_file():
            return self.default_state()
        try:
            data = json.loads(self.state_file.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise HarnessError(f"Invalid session state: {self.state_file}: {exc}") from exc
        state = self.default_state()
        state.update(data)
        return state

    def _save(self, state: dict[str, Any]) -> None:
        self.state_dir.mkdir(parents=True, exist_ok=True)
        state["updated_at"] = utc_now()
        temporary = self.state_file.with_suffix(".json.tmp")
        temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        os.replace(temporary, self.state_file)

    def configure(self, updates: dict[str, Any], dry_run: bool = False) -> dict[str, Any]:
        current = self.load()
        previous_config = dict(current["config"])
        next_config = dict(previous_config)
        next_config.update({key: value for key, value in updates.items() if value is not None})
        result = {"changed": previous_config != next_config, "before": previous_config, "after": next_config}
        if dry_run or not result["changed"]:
            result["dry_run"] = dry_run
            return result
        with FileLock(self.lock_file):
            current = self.load()
            previous_config = dict(current["config"])
            next_config = dict(previous_config)
            next_config.update({key: value for key, value in updates.items() if value is not None})
            current["history"] = [*current["history"], previous_config][-50:]
            current["future"] = []
            current["config"] = next_config
            self._save(current)
        result.update({"before": previous_config, "after": next_config, "dry_run": False})
        return result

    def undo(self, dry_run: bool = False) -> dict[str, Any]:
        return self._move_history("undo", dry_run)

    def redo(self, dry_run: bool = False) -> dict[str, Any]:
        return self._move_history("redo", dry_run)

    def _move_history(self, direction: str, dry_run: bool) -> dict[str, Any]:
        state = self.load()
        source_key, target_key = ("history", "future") if direction == "undo" else ("future", "history")
        if not state[source_key]:
            return {"changed": False, "direction": direction, "config": state["config"], "dry_run": dry_run}
        before = dict(state["config"])
        after = dict(state[source_key][-1])
        result = {"changed": True, "direction": direction, "before": before, "after": after, "dry_run": dry_run}
        if dry_run:
            return result
        with FileLock(self.lock_file):
            state = self.load()
            if not state[source_key]:
                return {"changed": False, "direction": direction, "config": state["config"], "dry_run": False}
            before = dict(state["config"])
            after = dict(state[source_key].pop())
            state[target_key] = [*state[target_key], before][-50:]
            state["config"] = after
            self._save(state)
        result.update({"before": before, "after": after})
        return result

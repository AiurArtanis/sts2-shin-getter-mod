#!/usr/bin/env python3
"""Build a reproducible 0.109 -> 0.111 CodeGraph and mod-reference audit.

The two game source trees and their CodeGraph databases are always opened read-only.
The generated JSON is the exhaustive machine-readable inventory; the Markdown file
is a bounded review summary plus every changed symbol that the mod may reference.
"""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import os
import re
import sqlite3
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


PROJECT_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PROJECT_ROOT.parent
FORMAL_ROOT = Path(
    os.environ.get("SHIN_GETTER_STS2_109_SOURCE", r"E:\Work\SlaytheSpare2")
)
BETA_ROOT = Path(
    os.environ.get("SHIN_GETTER_STS2_111_SOURCE", r"E:\Work\SlaytheSpare2-111-beta")
)
JSON_OUTPUT = REPO_ROOT / ".github/issue-93-109-vs-111-codegraph-diff.json"
MARKDOWN_OUTPUT = REPO_ROOT / ".github/issue-93-109-vs-111-codegraph-diff.md"

SYMBOL_KINDS = (
    "class",
    "interface",
    "struct",
    "enum",
    "enum_member",
    "method",
    "property",
    "field",
    "constant",
)
EDGE_KINDS = ("calls", "references", "instantiates", "extends", "implements")
TYPE_KINDS = {"class", "interface", "struct", "enum"}
GAME_PATH_PREFIXES = ("src/", "addons/")
IDENTIFIER_RE = re.compile(r"(?<![A-Za-z0-9_])[A-Za-z_][A-Za-z0-9_]*")
DYNAMIC_CALL_RE = re.compile(r"\b(HarmonyPatch|AccessTools\.[A-Za-z_][A-Za-z0-9_]*)")
STRING_NAME_RE = re.compile(r'"([A-Za-z_][A-Za-z0-9_]*)"')
NAMEOF_MEMBER_RE = re.compile(
    r"nameof\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*([A-Za-z_][A-Za-z0-9_]*)\s*\)"
)
NAMEOF_TARGET_RE = re.compile(
    r"nameof\s*\(\s*(?P<owner>(?:[A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*)\."
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)"
)
TYPEOF_RE = re.compile(r"typeof\s*\(\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)")
GENERIC_TARGET_RE = re.compile(
    r"AccessTools\.[A-Za-z_][A-Za-z0-9_]*\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)"
)
OVERRIDE_NAME_RE = re.compile(
    r"\boverride\s+(?:async\s+)?(?:[A-Za-z_][A-Za-z0-9_<>?,.\[\]\s]*\s+)"
    r"([A-Za-z_][A-Za-z0-9_]*)\s*\("
)
PROBE_PROJECT = REPO_ROOT / "tools/Issue93CompatibilityProbe/Issue93CompatibilityProbe.csproj"
MOD_ASSEMBLY = PROJECT_ROOT / "build/ShinGetterMod.dll"

REVIEW_CONCLUSIONS = {
    "MegaCrit.Sts2.Core.Entities.Cards::CardPile": (
        "compatible",
        "The mod only receives existing CardPile instances; it never constructs CardPile, so the new primary constructor is not crossed.",
    ),
    "MegaCrit.Sts2.Core.Saves.Runs::SavedPropertyAttribute": (
        "compatible",
        "0.111 restricts the attribute to properties; every mod use is already on a property.",
    ),
    "MegaCrit.Sts2.Core.Entities.Multiplayer::StartRunLobbyPlayer::character": (
        "adapted",
        "The character-select patch now receives StartRunLobbyPlayer and reads the retained character field.",
    ),
    "MegaCrit.Sts2.Core.Commands.Builders::AttackCommand::FromCard": (
        "adapted",
        "All 63 card-origin attack builders pass the active CardPlay context.",
    ),
    "MegaCrit.Sts2.Core.Commands::CardCmd::Exhaust": (
        "compatible",
        "Every caller awaits and ignores the result, so Task<CardPileAddResult?> is source- and behavior-compatible.",
    ),
    "MegaCrit.Sts2.Core.Commands::CardPileCmd::Add": (
        "compatible",
        "The mod uses retained CardModel/PileType and IEnumerable/PileType overloads, not the changed IEnumerable/CardPile overload.",
    ),
    "MegaCrit.Sts2.Core.Commands::CardPileCmd::Draw": (
        "compatible",
        "The used four-parameter signature is unchanged; only the decompiled async implementation shape differs.",
    ),
    "MegaCrit.Sts2.Core.Commands::CardSelectCmd::FromCombatPile": (
        "compatible",
        "The only declaration change is a nullable annotation on the filter; both used runtime signatures remain identical.",
    ),
    "MegaCrit.Sts2.Core.Commands::CreatureCmd::Damage": (
        "adapted",
        "Card and enchantment damage forwards CardPlay; power/event damage explicitly passes null, selecting the intended 0.111 overloads.",
    ),
    "MegaCrit.Sts2.Core.Commands::CreatureCmd::LoseBlock": (
        "adapted",
        "Both calls now pass PlayerChoiceContext and the correct remover (null for Avalanche, owner for Getter Chop).",
    ),
    "MegaCrit.Sts2.Core.Commands::PowerCmd::Apply": (
        "compatible",
        "The used generic single-target overload is unchanged; the changed collection overload only gains a nullable annotation.",
    ),
    "MegaCrit.Sts2.Core.Events::EventOption::EventOption": (
        "compatible",
        "0.111 only adds a copy constructor; all four constructor shapes used by the mod are retained.",
    ),
    "MegaCrit.Sts2.Core.Factories::PotionFactory::CreateRandomPotions": (
        "adapted",
        "The Harmony patch targets the plural private method and accepts IEnumerable<PotionModel>.",
    ),
    "MegaCrit.Sts2.Core.GameActions.Multiplayer::PlayerChoiceContext::SignalPlayerChoiceBegun": (
        "adapted",
        "The form picker supplies the actual choosing Player before PlayerChoiceOptions.",
    ),
    "MegaCrit.Sts2.Core.Hooks::Hook::ModifyDamage": (
        "adapted",
        "The named Harmony postfix subset remains bindable after CardPlay insertion; the runtime probe requires the unique 11-parameter target.",
    ),
    "MegaCrit.Sts2.Core.Models::AbstractModel::ModifyDamageAdditive": (
        "adapted",
        "All three additive overrides accept and forward the new CardPlay parameter.",
    ),
    "MegaCrit.Sts2.Core.Models::AbstractModel::ModifyDamageMultiplicative": (
        "adapted",
        "All five multiplicative overrides accept and forward the new CardPlay parameter.",
    ),
    "MegaCrit.Sts2.Core.Models::CharacterModel::GenerateAnimator": (
        "adapted",
        "ShinGetter implements GenerateAnimator(MegaSprite, Creature) without changing its custom animator behavior.",
    ),
    "MegaCrit.Sts2.Core.Models::EventModel::EnterCombatWithoutExitingEvent": (
        "compatible",
        "Only the first parameter name changes; the reflection lookup pins the unchanged three runtime parameter types.",
    ),
    "MegaCrit.Sts2.Core.Models::ModelId::ModelId": (
        "compatible",
        "Only the compiler-generated copy constructor disappears; the mod references the retained (string, string) constructor.",
    ),
    "MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect::NCharacterSelectScreen::PlayerChanged": (
        "adapted",
        "The Harmony postfix parameter type is StartRunLobbyPlayer, matching the 0.111 method exactly.",
    ),
    "MegaCrit.Sts2.Core.Runs.History::CardEnchantmentHistoryEntry::CardEnchantmentHistoryEntry": (
        "compatible",
        "The explicit constructor becomes an equivalent primary constructor with the same runtime (CardModel, ModelId) signature.",
    ),
    "MegaCrit.Sts2.Core.Runs::CardCreationOptions::ForNonCombatWithDefaultOdds": (
        "compatible",
        "The removed IEnumerable<CardModel> overload is unused; both calls use the retained CardPoolModel collection plus filter overload.",
    ),
    "MegaCrit.Sts2.Core.Entities.Ancients::AncientDialogueSet::CharacterDialogues": (
        "compatible",
        "The property becomes required for construction, but the mod only reads and mutates an already initialized game instance.",
    ),
    "MegaCrit.Sts2.Core.Entities.Multiplayer::StartRunLobbyPlayer": (
        "adapted",
        "This replacement for removed LobbyPlayer is used only at the 0.111 character-select patch boundary.",
    ),
    "MegaCrit.Sts2.Core.Runs.History::CardEnchantmentHistoryEntry": (
        "compatible",
        "The struct gains a primary constructor while preserving its IPacketSerializable contract and emitted constructor signature.",
    ),
}


@dataclass(frozen=True)
class Symbol:
    kind: str
    name: str
    qualified_name: str
    file_path: str
    start_line: int
    end_line: int
    visibility: str | None
    is_async: int
    is_static: int
    is_abstract: int
    decorators: str | None
    type_parameters: str | None
    declaration: str

    @property
    def group_key(self) -> tuple[str, str]:
        return self.kind, self.qualified_name

    @property
    def identity(self) -> tuple[object, ...]:
        return (
            self.declaration,
            self.visibility,
            self.is_async,
            self.is_static,
            self.is_abstract,
            self.decorators,
            self.type_parameters,
        )

    def as_dict(self) -> dict[str, object]:
        return {
            "kind": self.kind,
            "name": self.name,
            "qualified_name": self.qualified_name,
            "declaration": self.declaration,
            "visibility": self.visibility,
            "is_async": bool(self.is_async),
            "is_static": bool(self.is_static),
            "is_abstract": bool(self.is_abstract),
            "decorators": self.decorators,
            "type_parameters": self.type_parameters,
            "source": f"{self.file_path}:{self.start_line}",
        }


def readonly_connection(root: Path) -> sqlite3.Connection:
    database = root / ".codegraph/codegraph.db"
    if not database.is_file():
        raise FileNotFoundError(f"CodeGraph database is missing: {database}")
    return sqlite3.connect(f"file:{database}?mode=ro&immutable=1", uri=True)


def read_lines(root: Path, relative_path: str, cache: dict[str, list[str]]) -> list[str]:
    if relative_path not in cache:
        path = root / relative_path
        cache[relative_path] = path.read_text(
            encoding="utf-8-sig", errors="replace"
        ).splitlines()
    return cache[relative_path]


def declaration_for(
    root: Path,
    row: sqlite3.Row,
    cache: dict[str, list[str]],
) -> str:
    lines = read_lines(root, row["file_path"], cache)
    start = max(row["start_line"] - 1, 0)
    stop = min(len(lines), max(start + 1, min(row["end_line"], start + 40)))
    chunks: list[str] = []
    paren_depth = 0
    bracket_depth = 0

    for line in lines[start:stop]:
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        chunks.append(stripped)
        paren_depth += stripped.count("(") - stripped.count(")")
        bracket_depth += stripped.count("[") - stripped.count("]")
        if paren_depth > 0 or bracket_depth > 0:
            continue
        if row["kind"] in {"field", "constant", "enum_member"}:
            if ";" in stripped or stripped.endswith(","):
                break
        elif "{" in stripped or "=>" in stripped or stripped.endswith(";"):
            break

    declaration = " ".join(chunks)
    cut = len(declaration)
    for token in ("{", "=>"):
        index = declaration.find(token)
        if index >= 0:
            cut = min(cut, index)
    declaration = declaration[:cut].strip().rstrip(";").rstrip(",").strip()

    # Initializers of ordinary fields/readonly fields are implementation details.
    # Constant and enum values remain because callers may inline them.
    if row["kind"] in {"field", "constant"} and " const " not in f" {declaration} ":
        declaration = declaration.split("=", 1)[0].rstrip()
    declaration = re.sub(r"\s+", " ", declaration)
    return declaration


def load_files(root: Path) -> dict[str, dict[str, object]]:
    connection = readonly_connection(root)
    try:
        connection.row_factory = sqlite3.Row
        return {
            row["path"]: {
                "content_hash": row["content_hash"],
                "language": row["language"],
                "size": row["size"],
            }
            for row in connection.execute(
                "SELECT path, content_hash, language, size FROM files"
            )
        }
    finally:
        connection.close()


def load_symbols(root: Path) -> list[Symbol]:
    connection = readonly_connection(root)
    try:
        connection.row_factory = sqlite3.Row
        placeholders = ",".join("?" for _ in SYMBOL_KINDS)
        rows = connection.execute(
            f"""
            SELECT kind, name, qualified_name, file_path, start_line, end_line,
                   visibility, is_async, is_static, is_abstract, decorators,
                   type_parameters
              FROM nodes
             WHERE kind IN ({placeholders})
            """,
            SYMBOL_KINDS,
        ).fetchall()
    finally:
        connection.close()

    cache: dict[str, list[str]] = {}
    symbols: list[Symbol] = []
    for row in rows:
        if not row["file_path"].startswith(GAME_PATH_PREFIXES):
            continue
        symbols.append(
            Symbol(
                kind=row["kind"],
                name=row["name"],
                qualified_name=row["qualified_name"],
                file_path=row["file_path"],
                start_line=row["start_line"],
                end_line=row["end_line"],
                visibility=row["visibility"],
                is_async=row["is_async"],
                is_static=row["is_static"],
                is_abstract=row["is_abstract"],
                decorators=row["decorators"],
                type_parameters=row["type_parameters"],
                declaration=declaration_for(root, row, cache),
            )
        )
    return symbols


def load_edges(root: Path) -> set[tuple[str, str, str, str, str]]:
    connection = readonly_connection(root)
    try:
        placeholders = ",".join("?" for _ in EDGE_KINDS)
        rows = connection.execute(
            f"""
            SELECT DISTINCT edge.kind,
                            source.kind,
                            source.qualified_name,
                            target.kind,
                            target.qualified_name
              FROM edges AS edge
              JOIN nodes AS source ON source.id = edge.source
              JOIN nodes AS target ON target.id = edge.target
             WHERE edge.kind IN ({placeholders})
            """,
            EDGE_KINDS,
        ).fetchall()
        return set(rows)
    finally:
        connection.close()


def group_symbols(symbols: Iterable[Symbol]) -> dict[tuple[str, str], list[Symbol]]:
    grouped: dict[tuple[str, str], list[Symbol]] = collections.defaultdict(list)
    for symbol in symbols:
        grouped[symbol.group_key].append(symbol)
    for values in grouped.values():
        values.sort(key=lambda symbol: (symbol.identity, symbol.file_path, symbol.start_line))
    return dict(grouped)


def normalized_group(values: list[Symbol]) -> collections.Counter[tuple[object, ...]]:
    return collections.Counter(value.identity for value in values)


def dynamic_calls(text: str, relative: str) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    for match in DYNAMIC_CALL_RE.finditer(text):
        cursor = match.end()
        while cursor < len(text) and text[cursor].isspace():
            cursor += 1
        if cursor < len(text) and text[cursor] == "<":
            depth = 0
            while cursor < len(text):
                char = text[cursor]
                depth += char == "<"
                depth -= char == ">"
                cursor += 1
                if depth == 0:
                    break
            while cursor < len(text) and text[cursor].isspace():
                cursor += 1
        if cursor >= len(text) or text[cursor] != "(":
            continue

        start = cursor
        depth = 0
        quote: str | None = None
        escaped = False
        while cursor < len(text):
            char = text[cursor]
            if quote is not None:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == quote:
                    quote = None
            elif char in {'"', "'"}:
                quote = char
            elif char == "(":
                depth += 1
            elif char == ")":
                depth -= 1
                if depth == 0:
                    cursor += 1
                    break
            cursor += 1

        snippet = text[match.start():cursor]
        names = sorted(set(STRING_NAME_RE.findall(snippet) + NAMEOF_MEMBER_RE.findall(snippet)))
        target_types = TYPEOF_RE.findall(snippet)[:1]
        nameof_targets = NAMEOF_TARGET_RE.findall(snippet)
        if not target_types and nameof_targets:
            target_types = [nameof_targets[0][0]]
        if not target_types:
            generic_target = GENERIC_TARGET_RE.search(snippet)
            if generic_target:
                target_types = [generic_target.group(1)]
        line_number = text.count("\n", 0, match.start()) + 1
        records.append(
            {
                "api": match.group(1),
                "location": f"{relative}:{line_number}",
                "target_names": names,
                "target_type_names": target_types,
                "snippet": re.sub(r"\s+", " ", snippet).strip(),
            }
        )
    return records


def inventory_mod_source() -> tuple[
    list[dict[str, object]],
    dict[str, list[str]],
    list[dict[str, object]],
    set[str],
]:
    files: list[dict[str, object]] = []
    identifier_locations: dict[str, list[str]] = collections.defaultdict(list)
    target_calls: list[dict[str, object]] = []
    override_names: set[str] = set()
    roots = (PROJECT_ROOT / "src", REPO_ROOT / "tools")

    for root in roots:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.cs")):
            if any(part in {"bin", "obj"} for part in path.parts):
                continue
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            relative = path.relative_to(REPO_ROOT).as_posix()
            files.append(
                {
                    "path": relative,
                    "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest().upper(),
                    "line_count": len(text.splitlines()),
                }
            )
            for line_number, line in enumerate(text.splitlines(), 1):
                for identifier in set(IDENTIFIER_RE.findall(line)):
                    locations = identifier_locations[identifier]
                    if len(locations) < 12:
                        locations.append(f"{relative}:{line_number}")
            target_calls.extend(dynamic_calls(text, relative))
            override_names.update(OVERRIDE_NAME_RE.findall(text))

    return files, dict(identifier_locations), target_calls, override_names


def inventory_mod_binary() -> dict[str, object]:
    if not MOD_ASSEMBLY.is_file():
        raise FileNotFoundError(
            f"Build the complete Beta mod before running the reference audit: {MOD_ASSEMBLY}"
        )
    if not PROBE_PROJECT.is_file():
        raise FileNotFoundError(f"Reference inventory probe is missing: {PROBE_PROJECT}")
    environment = os.environ.copy()
    result = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(PROBE_PROJECT),
            "--configuration",
            "Release",
            "--",
            "--inventory",
            str(MOD_ASSEMBLY),
        ],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        details = "\n".join((result.stdout + "\n" + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"issue#93 binary reference inventory failed:\n{details}")
    output_lines = [line for line in result.stdout.splitlines() if line.strip()]
    try:
        inventory = json.loads(output_lines[-1])
        inventory["assembly"] = MOD_ASSEMBLY.relative_to(REPO_ROOT).as_posix()
        return inventory
    except (IndexError, json.JSONDecodeError) as error:
        raise RuntimeError(
            f"issue#93 reference inventory returned invalid JSON: {result.stdout[-2000:]}"
        ) from error


def binary_type_name(codegraph_name: str) -> str:
    namespace, *types = codegraph_name.split("::")
    if not types:
        return namespace
    return f"{namespace}.{types[0]}" + "".join(f"+{value}" for value in types[1:])


def symbol_binary_owner(symbol: Symbol) -> str | None:
    if symbol.kind in TYPE_KINDS:
        return binary_type_name(symbol.qualified_name)
    if "::" not in symbol.qualified_name:
        return None
    return binary_type_name(symbol.qualified_name.rsplit("::", 1)[0])


def candidate_reason(
    symbol: Symbol,
    identifier_locations: dict[str, list[str]],
    dynamic_pairs: set[tuple[str, str]],
    unscoped_dynamic_names: set[str],
    binary_types: set[str],
    binary_members: set[tuple[str, str]],
    binary_overrides: set[tuple[str, str]],
) -> tuple[str, list[str]] | None:
    owner = symbol_binary_owner(symbol)
    locations = identifier_locations.get(symbol.name, [])
    if symbol.kind in TYPE_KINDS and owner in binary_types:
        return "binary-type-reference", locations
    member_names = {symbol.name}
    if symbol.kind == "property":
        member_names.update((f"get_{symbol.name}", f"set_{symbol.name}"))
    if symbol.kind == "method" and owner and owner.rsplit("+", 1)[-1].rsplit(".", 1)[-1] == symbol.name:
        member_names.add(".ctor")
    if owner and any((owner, name) in binary_members for name in member_names):
        return "binary-member-reference", locations
    owner_name = owner.rsplit("+", 1)[-1].rsplit(".", 1)[-1] if owner else ""
    if (owner_name, symbol.name) in dynamic_pairs or symbol.name in unscoped_dynamic_names:
        return "dynamic-target-name", locations
    if owner and (owner, symbol.name) in binary_overrides:
        return "binary-override", locations
    return None


def public_api(symbol: Symbol) -> bool:
    return symbol.visibility in {"public", "protected", "protected internal"}


def generated_noise(symbol: Symbol) -> bool:
    return any(
        marker in symbol.file_path
        for marker in (
            "AbstractModelSubtypes.cs",
            ".g.cs",
            ".generated.cs",
            "Generated/",
        )
    )


def build_inventory() -> dict[str, object]:
    formal_files = load_files(FORMAL_ROOT)
    beta_files = load_files(BETA_ROOT)
    formal_symbols = group_symbols(load_symbols(FORMAL_ROOT))
    beta_symbols = group_symbols(load_symbols(BETA_ROOT))
    formal_edges = load_edges(FORMAL_ROOT)
    beta_edges = load_edges(BETA_ROOT)
    mod_files, identifier_locations, target_calls, override_names = inventory_mod_source()
    dynamic_names = {
        name
        for call in target_calls
        for name in call["target_names"]
    }
    dynamic_pairs = {
        (target_type.rsplit(".", 1)[-1], name)
        for call in target_calls
        for target_type in call["target_type_names"]
        for name in call["target_names"]
    }
    unscoped_dynamic_names = {
        name
        for call in target_calls
        if not call["target_type_names"]
        for name in call["target_names"]
    }
    binary_inventory = inventory_mod_binary()
    binary_types = set(binary_inventory["game_type_references"])
    binary_members = {
        (item["owner"], item["name"])
        for item in binary_inventory["game_member_references"]
    }
    binary_overrides = {
        (item["base_owner"], item["base_method"])
        for item in binary_inventory["game_overrides"]
    }

    formal_paths = set(formal_files)
    beta_paths = set(beta_files)
    shared_paths = formal_paths & beta_paths
    files_added = sorted(beta_paths - formal_paths)
    files_removed = sorted(formal_paths - beta_paths)
    files_changed = sorted(
        path
        for path in shared_paths
        if formal_files[path]["content_hash"] != beta_files[path]["content_hash"]
    )

    formal_keys = set(formal_symbols)
    beta_keys = set(beta_symbols)
    removed_keys = formal_keys - beta_keys
    added_keys = beta_keys - formal_keys
    changed_keys = {
        key
        for key in formal_keys & beta_keys
        if normalized_group(formal_symbols[key]) != normalized_group(beta_symbols[key])
    }

    def entry_for(
        key: tuple[str, str],
        change: str,
    ) -> dict[str, object]:
        formal = formal_symbols.get(key, [])
        beta = beta_symbols.get(key, [])
        representative = (beta or formal)[0]
        reason = candidate_reason(
            representative,
            identifier_locations,
            dynamic_pairs,
            unscoped_dynamic_names,
            binary_types,
            binary_members,
            binary_overrides,
        )
        return {
            "change": change,
            "kind": key[0],
            "qualified_name": key[1],
            "formal": [symbol.as_dict() for symbol in formal],
            "beta": [symbol.as_dict() for symbol in beta],
            "public_api": any(public_api(symbol) for symbol in formal + beta),
            "generated_noise": all(generated_noise(symbol) for symbol in formal + beta),
            "mod_candidate": reason is not None,
            "candidate_reason": reason[0] if reason else None,
            "mod_locations": reason[1] if reason else [],
        }

    symbol_changes = [entry_for(key, "removed") for key in sorted(removed_keys)]
    symbol_changes += [entry_for(key, "added") for key in sorted(added_keys)]
    symbol_changes += [entry_for(key, "signature") for key in sorted(changed_keys)]
    symbol_changes.sort(key=lambda item: (item["kind"], item["qualified_name"], item["change"]))

    counts_by_kind: dict[str, dict[str, int]] = {}
    for kind in SYMBOL_KINDS:
        counts_by_kind[kind] = {
            change: sum(
                item["kind"] == kind and item["change"] == change
                for item in symbol_changes
            )
            for change in ("removed", "added", "signature")
        }

    candidate_changes = [item for item in symbol_changes if item["mod_candidate"]]
    candidate_names = {item["qualified_name"] for item in candidate_changes}
    expected_names = set(REVIEW_CONCLUSIONS)
    if candidate_names != expected_names:
        missing = sorted(candidate_names - expected_names)
        stale = sorted(expected_names - candidate_names)
        raise RuntimeError(
            "issue#93 changed-symbol review map drifted; "
            f"unreviewed={missing}, stale={stale}"
        )
    for item in candidate_changes:
        status, conclusion = REVIEW_CONCLUSIONS[item["qualified_name"]]
        item["review_status"] = status
        item["review_conclusion"] = conclusion
    public_changes = [item for item in symbol_changes if item["public_api"]]
    implementation_only_files = sorted(
        set(files_changed)
        - {
            source["source"].rsplit(":", 1)[0]
            for item in symbol_changes
            for side in ("formal", "beta")
            for source in item[side]
        }
    )
    edges_removed = sorted(formal_edges - beta_edges)
    edges_added = sorted(beta_edges - formal_edges)

    def edge_record(edge: tuple[str, str, str, str, str]) -> dict[str, str]:
        kind, source_kind, source, target_kind, target = edge
        return {
            "kind": kind,
            "source_kind": source_kind,
            "source": source,
            "target_kind": target_kind,
            "target": target,
        }

    return {
        "schema_version": 1,
        "inputs": {
            "formal_root": str(FORMAL_ROOT),
            "formal_codegraph_db": str(FORMAL_ROOT / ".codegraph/codegraph.db"),
            "beta_root": str(BETA_ROOT),
            "beta_codegraph_db": str(BETA_ROOT / ".codegraph/codegraph.db"),
            "repo_root": ".",
        },
        "file_diff": {
            "formal_count": len(formal_files),
            "beta_count": len(beta_files),
            "shared_count": len(shared_paths),
            "added": files_added,
            "removed": files_removed,
            "changed": files_changed,
            "implementation_only_changed": implementation_only_files,
        },
        "symbol_diff": {
            "formal_count": sum(len(values) for values in formal_symbols.values()),
            "beta_count": sum(len(values) for values in beta_symbols.values()),
            "counts_by_kind": counts_by_kind,
            "changes": symbol_changes,
            "public_change_count": len(public_changes),
            "mod_candidate_count": len(candidate_changes),
        },
        "relation_diff": {
            "formal_count": len(formal_edges),
            "beta_count": len(beta_edges),
            "counts_by_kind": {
                kind: {
                    "formal": sum(edge[0] == kind for edge in formal_edges),
                    "beta": sum(edge[0] == kind for edge in beta_edges),
                    "removed": sum(edge[0] == kind for edge in edges_removed),
                    "added": sum(edge[0] == kind for edge in edges_added),
                }
                for kind in EDGE_KINDS
            },
            "removed": [edge_record(edge) for edge in edges_removed],
            "added": [edge_record(edge) for edge in edges_added],
        },
        "mod_source_inventory": {
            "file_count": len(mod_files),
            "line_count": sum(int(item["line_count"]) for item in mod_files),
            "files": mod_files,
            "identifier_count": len(identifier_locations),
            "dynamic_target_names": sorted(dynamic_names),
            "dynamic_target_call_count": len(target_calls),
            "dynamic_target_calls": target_calls,
            "override_names": sorted(override_names),
        },
        "mod_binary_reference_inventory": binary_inventory,
    }


def declaration_text(items: list[dict[str, object]]) -> str:
    values = [f"`{item['declaration']}`" for item in items]
    return "<br>".join(values) if values else "—"


def markdown_for(inventory: dict[str, object]) -> str:
    file_diff = inventory["file_diff"]
    symbol_diff = inventory["symbol_diff"]
    relation_diff = inventory["relation_diff"]
    mod_inventory = inventory["mod_source_inventory"]
    binary_inventory = inventory["mod_binary_reference_inventory"]
    changes = symbol_diff["changes"]
    public_changes = [item for item in changes if item["public_api"]]
    candidates = [item for item in changes if item["mod_candidate"]]

    lines = [
        "# issue#93 — 0.109 vs 0.111 Beta CodeGraph mechanical audit",
        "",
        "This file is generated by `shin-getter-mod-godot/tools/audit_issue_93_codegraph.py`.",
        "The paired JSON file is the exhaustive inventory; this Markdown keeps the review surface bounded.",
        "Both CodeGraph databases are opened with SQLite `mode=ro&immutable=1`.",
        "",
        "## Scope and totals",
        "",
        f"- Formal CodeGraph: {file_diff['formal_count']} files; game symbols inventoried: {symbol_diff['formal_count']}.",
        f"- 0.111 Beta CodeGraph: {file_diff['beta_count']} files; game symbols inventoried: {symbol_diff['beta_count']}.",
        f"- File paths: {file_diff['shared_count']} shared, {len(file_diff['added'])} added, {len(file_diff['removed'])} removed, {len(file_diff['changed'])} content-changed.",
        f"- Symbol groups: {len(changes)} added/removed/declaration-changed; {len(public_changes)} touch public/protected API.",
        f"- Full mod traversal: {mod_inventory['file_count']} C# files, {mod_inventory['line_count']} lines, {mod_inventory['identifier_count']} identifiers, {mod_inventory['dynamic_target_call_count']} Harmony/reflection calls with {len(mod_inventory['dynamic_target_names'])} distinct target names.",
        f"- Compiled mod references: {binary_inventory['game_type_reference_count']} game types, {binary_inventory['direct_game_member_reference_count']} direct game members and {binary_inventory['generic_game_member_reference_count']} members on constructed generic game types.",
        f"- Mechanical changed-symbol candidates found in mod source: {len(candidates)}. Every candidate appears below and in JSON with source locations.",
        "",
        "## Symbol difference counts",
        "",
        "| Kind | Removed | Added | Declaration changed |",
        "| --- | ---: | ---: | ---: |",
    ]
    for kind in SYMBOL_KINDS:
        counts = symbol_diff["counts_by_kind"][kind]
        lines.append(
            f"| `{kind}` | {counts['removed']} | {counts['added']} | {counts['signature']} |"
        )

    lines.extend(
        [
            "",
            "## Code relation difference counts",
            "",
            "These are distinct CodeGraph relations after removing line-number noise.",
            "",
            "| Relation | 0.109 | 0.111 | Removed | Added |",
            "| --- | ---: | ---: | ---: | ---: |",
        ]
    )
    for kind in EDGE_KINDS:
        counts = relation_diff["counts_by_kind"][kind]
        lines.append(
            f"| `{kind}` | {counts['formal']} | {counts['beta']} | {counts['removed']} | {counts['added']} |"
        )

    lines.extend(
        [
            "",
            "## Changed symbols referenced or named by the mod",
            "",
            "Candidates come from compiled game TypeRef/MemberRef metadata plus Harmony/reflection names. Compilation and the runtime probe decide compatibility; dynamic names can still be over-inclusive.",
            "",
            "| Change | Kind | Symbol | 0.109 declaration | 0.111 declaration | Review | First mod locations |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        ]
    )
    for item in candidates:
        locations = "<br>".join(f"`{value}`" for value in item["mod_locations"][:4]) or "—"
        lines.append(
            "| {change} | `{kind}` | `{name}` | {formal} | {beta} | **{status}** — {conclusion} | {locations} |".format(
                change=item["change"],
                kind=item["kind"],
                name=item["qualified_name"],
                formal=declaration_text(item["formal"]),
                beta=declaration_text(item["beta"]),
                status=item["review_status"],
                conclusion=item["review_conclusion"],
                locations=locations,
            )
        )

    lines.extend(
        [
            "",
            "## Complete file lists",
            "",
            "The exhaustive added, removed, content-changed, implementation-only and symbol records are stored in:",
            "",
            "- `.github/issue-93-109-vs-111-codegraph-diff.json`",
            "",
            "The JSON also records the SHA-256 and line count of every traversed mod C# file, allowing the traversal scope to be reproduced exactly.",
            "",
        ]
    )
    return "\n".join(lines)


def serialize(inventory: dict[str, object]) -> tuple[str, str]:
    json_text = json.dumps(inventory, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    markdown_text = markdown_for(inventory)
    return json_text, markdown_text


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail if committed audit outputs differ from a fresh read-only audit",
    )
    args = parser.parse_args()
    inventory = build_inventory()
    json_text, markdown_text = serialize(inventory)

    if args.check:
        expected = ((JSON_OUTPUT, json_text), (MARKDOWN_OUTPUT, markdown_text))
        stale = [str(path) for path, text in expected if not path.is_file() or path.read_text(encoding="utf-8") != text]
        if stale:
            raise SystemExit("issue#93 CodeGraph audit output is stale: " + ", ".join(stale))
        print(
            "issue#93 CodeGraph audit check passed: "
            f"{inventory['mod_source_inventory']['file_count']} mod C# files; "
            f"{inventory['symbol_diff']['mod_candidate_count']} changed-symbol candidates"
        )
        return

    JSON_OUTPUT.write_text(json_text, encoding="utf-8", newline="\n")
    MARKDOWN_OUTPUT.write_text(markdown_text, encoding="utf-8", newline="\n")
    print(f"wrote {JSON_OUTPUT}")
    print(f"wrote {MARKDOWN_OUTPUT}")


if __name__ == "__main__":
    main()

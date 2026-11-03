"""Register editable player originals, retain revisions, and report pending game exports.

Source registration never edits Unity assets or changes combat/animation timing.
Run with --help for registration, usage records, validation and listing.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import tempfile
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LIBRARY = ROOT / "ArtSource/Player"
DEFAULT_ASEPRITE = r"C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe"
STATUSES = {"received": "受領済み", "provisional": "仮提出", "sample": "既存サンプル", "reference": "参考"}


class LibraryError(ValueError):
    pass


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(prefix=".pending-", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(handle, "wb") as file:
            file.write(data)
            file.flush()
            os.fsync(file.fileno())
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def inspect_document(data: bytes, aseprite: str = DEFAULT_ASEPRITE) -> dict:
    if len(data) < 128 or struct.unpack_from("<H", data, 4)[0] != 0xA5E0:
        raise LibraryError("Not an Aseprite document")
    if struct.unpack_from("<I", data)[0] != len(data):
        raise LibraryError("Aseprite file size does not match its header")
    scratch = ROOT / ".uloop/outputs"
    scratch.mkdir(parents=True, exist_ok=True)
    if not scratch.resolve().is_relative_to(ROOT):
        raise LibraryError("Inspection scratch folder escaped the workspace")
    with tempfile.TemporaryDirectory(prefix="player-source-inspect-", dir=scratch) as folder:
        # TemporaryDirectory removes recursively; check its resolved boundary first.
        temporary = Path(folder).resolve()
        if not temporary.is_relative_to(scratch.resolve()):
            raise LibraryError("Inspection folder escaped the workspace")
        source, output = temporary / "source.aseprite", temporary / "inspection.json"
        source.write_bytes(data)
        command = [aseprite, "--batch", "--script-param", "input=" + str(source),
                   "--script-param", "output=" + str(output), "--script",
                   str(ROOT / "Tools/Art/inspect-player-source.lua")]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8",
                                errors="replace", timeout=90,
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        if result.returncode or not output.exists():
            raise LibraryError("Aseprite inspection failed: " + result.stdout + result.stderr)
        metadata = json.loads(output.read_text(encoding="utf-8"))
    expected_frames, width, height = struct.unpack_from("<HHH", data, 6)
    if (metadata["width"], metadata["height"], len(metadata["frames"])) != (width, height, expected_frames):
        raise LibraryError("Native inspection does not match the source header")
    return metadata


class PlayerSourceLibrary:
    def __init__(self, root: Path = DEFAULT_LIBRARY, inspector=inspect_document):
        self.root = root.resolve()
        if not self.root.is_relative_to(ROOT):
            raise LibraryError("Keep the library inside the project workspace")
        self.inspector = inspector
        self.catalog_path = self.root / "catalog.json"

    def path(self, relative: str) -> Path:
        result = (self.root / relative).resolve()
        if not result.is_relative_to(self.root) or result == self.root:
            raise LibraryError("Library path escapes its root: " + relative)
        return result

    @contextmanager
    def writing(self):
        self.root.mkdir(parents=True, exist_ok=True)
        lock = self.root / ".catalog.lock"
        try:
            handle = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        except FileExistsError as error:
            raise LibraryError("Another registration owns .catalog.lock; do not run writers together") from error
        try:
            with os.fdopen(handle, "w", encoding="utf-8") as file:
                file.write(str(os.getpid()))
            yield
        finally:
            lock.unlink(missing_ok=True)

    def load(self) -> dict:
        if not self.catalog_path.exists():
            return {"schema_version": 1, "motions": {}, "references": []}
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8-sig"))
        if catalog.get("schema_version") != 1 or not isinstance(catalog.get("motions"), dict):
            raise LibraryError("Unsupported player source catalog")
        return catalog

    def save(self, catalog: dict) -> None:
        atomic_write(self.catalog_path, (json.dumps(catalog, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
        atomic_write(self.root / "INDEX.md", self.render_index(catalog).encode("utf-8"))

    @staticmethod
    def check_id(motion_id: str) -> None:
        if not re.fullmatch(r"[a-z][a-z0-9_]{0,47}", motion_id):
            raise LibraryError("Use a stable lowercase motion id, e.g. attack_1")

    def check_history(self, entry: dict) -> None:
        revisions = entry["revisions"]
        if len({r["sha256"] for r in revisions}) != len(revisions):
            raise LibraryError("Duplicate revision in " + entry["name"])
        for revision in revisions:
            path = self.path(revision["file"])
            if not path.is_file() or digest(path.read_bytes()) != revision["sha256"]:
                raise LibraryError("Missing or changed immutable revision: " + revision["file"])

    def preserve(self, entry: dict, motion_id: str, data: bytes, filename: str, kind: str,
                 received_on: str | None = None, source_path: str | None = None) -> dict:
        sha = digest(data)
        existing = next((r for r in entry["revisions"] if r["sha256"] == sha), None)
        if existing is not None:
            return existing
        metadata = self.inspector(data)
        relative = f"History/{motion_id}/{sha}.aseprite"
        target = self.path(relative)
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            if target.read_bytes() != data:
                raise LibraryError("Refusing to overwrite a conflicting history snapshot")
        else:
            with target.open("xb") as file:
                file.write(data)
        revision = {"sha256": sha, "file": relative, "original_name": filename,
                    "registered_at": now(), "received_on": received_on,
                    "kind": kind, "metadata": metadata}
        if source_path:
            revision["original_project_path"] = source_path
        entry["revisions"].append(revision)
        return revision

    def register(self, motion_id: str, source: Path, *, name: str | None = None,
                 status: str | None = None, notes: str | None = None,
                 received_on: str | None = None) -> dict:
        self.check_id(motion_id)
        source = source.resolve()
        if not source.is_file() or source.suffix.lower() not in {".aseprite", ".ase"}:
            raise LibraryError("Supply an existing .aseprite or .ase source")
        data = source.read_bytes()  # Snapshot the input before inspection or any write.
        if received_on:
            from datetime import date
            date.fromisoformat(received_on)
        if status is not None and status not in STATUSES:
            raise LibraryError("Unknown material status")
        with self.writing():
            catalog = self.load()
            entry = catalog["motions"].get(motion_id)
            if entry is None:
                if not name:
                    raise LibraryError("A new motion needs --name")
                current = "Current/" + source.name
                if any(e["current"].casefold() == current.casefold() for e in catalog["motions"].values()):
                    raise LibraryError("This filename is already assigned to another motion")
                if self.path(current).exists():
                    raise LibraryError("An unregistered file already occupies the Current path")
                entry = {"name": name, "status": status or "provisional", "notes": notes or "",
                         "current": current, "current_sha256": None, "revisions": [], "uses": []}
                catalog["motions"][motion_id] = entry
            self.check_history(entry)
            current = self.path(entry["current"])
            local_data = current.read_bytes() if current.exists() else None
            if local_data is not None and digest(local_data) != entry["current_sha256"] and local_data != data:
                self.preserve(entry, motion_id, local_data, current.name, "saved-working-copy")
            original_path = source.relative_to(ROOT).as_posix() if source.is_relative_to(ROOT) else None
            revision = self.preserve(entry, motion_id, data, source.name, "submission", received_on, original_path)
            changed = entry["current_sha256"] != revision["sha256"]
            entry["name"] = name if name is not None else entry["name"]
            entry["status"] = status if status is not None else entry["status"]
            entry["notes"] = notes if notes is not None else entry["notes"]
            entry["current_sha256"] = revision["sha256"]
            if (current.read_bytes() if current.exists() else None) != local_data:
                raise LibraryError("Current changed during registration; keeping that newer edit, please retry")
            if local_data != data:
                atomic_write(current, data)
            self.save(catalog)
            return {"id": motion_id, "changed": changed, "sha256": revision["sha256"],
                    "revisions": len(entry["revisions"]), "current": entry["current"],
                    "canvas": [revision["metadata"]["width"], revision["metadata"]["height"]],
                    "frames": len(revision["metadata"]["frames"]),
                    "needs_game_update": [u["consumer"] for u in entry["uses"]
                                          if u["source_sha256"] != entry["current_sha256"]]}

    def record_use(self, motion_id: str, consumer: str, motion: str, references: list[str], evidence: str) -> None:
        self.check_id(motion_id)
        if not consumer.strip() or not motion.strip() or not references or not evidence:
            raise LibraryError("Record a consumer, motion, references and validation evidence")
        for relative in [*references, evidence]:
            path = (ROOT / relative).resolve()
            if not path.is_relative_to(ROOT) or not path.is_file():
                raise LibraryError("Missing workspace reference: " + relative)
        with self.writing():
            catalog = self.load()
            if motion_id not in catalog["motions"]:
                raise LibraryError("Register the source before recording a use")
            entry = catalog["motions"][motion_id]
            self.check_history(entry)
            if digest(self.path(entry["current"]).read_bytes()) != entry["current_sha256"]:
                raise LibraryError("Register the edited Current source before recording its use")
            entry["uses"] = [u for u in entry["uses"] if u["consumer"] != consumer]
            entry["uses"].append({"consumer": consumer, "motion": motion,
                                  "source_sha256": entry["current_sha256"],
                                  "references": references, "evidence": evidence})
            self.save(catalog)

    def status(self) -> dict:
        if not self.catalog_path.exists():
            return {"motions": [], "errors": ["catalog.json is missing"], "valid": False}
        catalog = self.load()
        rows, errors = [], []
        for motion_id, entry in catalog["motions"].items():
            try:
                self.check_id(motion_id)
                self.check_history(entry)
                if entry["status"] not in STATUSES:
                    raise LibraryError("Unknown material status")
                current = self.path(entry["current"])
                edited = not current.is_file() or digest(current.read_bytes()) != entry["current_sha256"]
                if not any(r["sha256"] == entry["current_sha256"] for r in entry["revisions"]):
                    raise LibraryError("Current version has no history snapshot")
                stale = [u["consumer"] for u in entry["uses"] if u["source_sha256"] != entry["current_sha256"]]
                for use in entry["uses"]:
                    if not any(r["sha256"] == use["source_sha256"] for r in entry["revisions"]):
                        raise LibraryError("Game use points to an unknown source revision")
                    for relative in [*use["references"], use["evidence"]]:
                        reference = (ROOT / relative).resolve()
                        if not reference.is_relative_to(ROOT) or not reference.is_file():
                            raise LibraryError("Missing game use reference: " + relative)
                if edited:
                    errors.append(motion_id + ": Current has unregistered changes or is missing")
                rows.append({"id": motion_id, "name": entry["name"], "status": entry["status"],
                             "current": entry["current"], "current_edited": edited,
                             "needs_game_update": stale, "revisions": len(entry["revisions"])})
            except (LibraryError, KeyError, OSError) as error:
                errors.append(motion_id + ": " + str(error))
        for item in catalog.get("references", []):
            path = (ROOT / item["path"]).resolve()
            if not path.is_relative_to(ROOT) or not path.exists():
                errors.append("Missing library reference: " + item["path"])
        return {"motions": rows, "errors": errors, "valid": not errors}

    def render_index(self, catalog: dict) -> str:
        def escaped(value):
            return str(value).replace("\\", "\\\\").replace("|", "\\|").replace("[", "\\[").replace("]", "\\]").replace("\n", " ")
        lines = ["# プレイヤー素材一覧", "", "[運用・追加更新](README.md) / [機械可読の台帳](catalog.json)", "",
                 "`Current/` が現在の編集用原本。`History/` は変更しない履歴。コマ数と時間はAseprite原稿の値です。", "",
                 "| 動作 | 現在版 | 区分 | 原稿 | 使用版の記録 | 履歴 |", "| --- | --- | --- | --- | --- | --- |"]
        for motion_id, entry in catalog["motions"].items():
            revision = next(r for r in entry["revisions"] if r["sha256"] == entry["current_sha256"])
            meta = revision["metadata"]
            uses = ", ".join(escaped(u["consumer"]) + ("：登録版一致" if u["source_sha256"] == entry["current_sha256"] else "：更新待ち") for u in entry["uses"]) or "使用記録なし"
            native = f'{meta["width"]}×{meta["height"]} / {len(meta["frames"])}コマ / {sum(f["duration_ms"] for f in meta["frames"])}ms'
            lines.append(f'| {escaped(entry["name"])} (`{motion_id}`) | [{escaped(Path(entry["current"]).name)}]({quote(entry["current"])}) | {STATUSES[entry["status"]]} | {native} | {uses} | [{len(entry["revisions"])}版](History/{motion_id}/) |')
        lines.extend(["", "## 素材ごとの注意", ""])
        for motion_id, entry in catalog["motions"].items():
            if entry["notes"]:
                lines.append(f'- **{escaped(entry["name"])}**：{escaped(entry["notes"])}')
        lines.extend(["", "## Codex製ラフ・既存の書き出し", "", "以下は受領原本と区別して保持します。", ""])
        for item in catalog.get("references", []):
            lines.append(f'- [{escaped(item["name"])}]({quote("../../" + item["path"])})：{escaped(item["notes"])}')
        return "\n".join(lines) + "\n"


def main() -> int:
    # JSON output is UTF-8 also when invoked from a Windows pipe.
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--aseprite", default=DEFAULT_ASEPRITE)
    commands = parser.add_subparsers(dest="command", required=True)
    register = commands.add_parser("register", help="Copy a new/updated source and preserve every previous version")
    register.add_argument("--id", required=True)
    register.add_argument("--source", type=Path, required=True)
    register.add_argument("--name")
    register.add_argument("--status", choices=STATUSES)
    register.add_argument("--notes")
    register.add_argument("--received-on")
    use = commands.add_parser("record-use", help="Record a source revision AFTER game integration and validation")
    use.add_argument("--id", required=True)
    use.add_argument("--consumer", required=True)
    use.add_argument("--motion", required=True)
    use.add_argument("--reference", action="append", required=True)
    use.add_argument("--evidence", required=True)
    commands.add_parser("status", help="Read-only check of registered sources and game versions")
    commands.add_parser("validate", help="Fail if originals/history or the generated index do not match the catalog")
    args = parser.parse_args()
    library = PlayerSourceLibrary(inspector=lambda data: inspect_document(data, args.aseprite))
    try:
        if args.command == "register":
            result = library.register(args.id, args.source, name=args.name, status=args.status,
                                      notes=args.notes, received_on=args.received_on)
        elif args.command == "record-use":
            library.record_use(args.id, args.consumer, args.motion, args.reference, args.evidence)
            result = {"recorded": args.id, "consumer": args.consumer}
        else:
            result = library.status()
            if args.command == "validate" and library.catalog_path.exists():
                index = library.root / "INDEX.md"
                if not index.is_file() or index.read_text(encoding="utf-8") != library.render_index(library.load()):
                    result["errors"].append("INDEX.md is not synchronized with catalog.json")
                    result["valid"] = False
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result.get("valid", True) else 1
    except (LibraryError, OSError, ValueError, subprocess.SubprocessError) as error:
        parser.exit(2, str(error) + "\n")


if __name__ == "__main__":
    raise SystemExit(main())

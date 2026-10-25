"""Exercise revision retention and update detection without touching production assets."""
from pathlib import Path
import subprocess
import tempfile
import unittest

from player_source_library import DEFAULT_ASEPRITE, LibraryError, PlayerSourceLibrary, ROOT, digest, inspect_document


def fake_inspect(data):
    return {"width": 64, "height": 64, "frames": [{"frame": 1, "duration_ms": len(data)}],
            "layers": [{"name": "Body", "visible": True, "opacity": 255}], "tags": []}


class PlayerSourceLibraryTests(unittest.TestCase):
    def setUp(self):
        scratch = ROOT / ".uloop/outputs"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="player-source-test-", dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name).resolve()
        # Validate the recursive cleanup boundary before any fixture is created.
        self.assertTrue(self.folder.is_relative_to(scratch.resolve()))
        self.source = self.folder / "Player_attack_1.aseprite"
        self.source.write_bytes(b"first source")
        self.library = PlayerSourceLibrary(self.folder / "library", inspector=fake_inspect)

    def register(self, source=None, **kwargs):
        return self.library.register("attack_1", source or self.source, **kwargs)

    def first(self):
        self.register(name="攻撃1", status="provisional")
        return self.library.load()["motions"]["attack_1"]

    def test_first_registration_preserves_source_and_native_copy(self):
        before = self.source.read_bytes()
        entry = self.first()
        self.assertEqual(before, self.source.read_bytes())
        self.assertEqual(before, self.library.path(entry["current"]).read_bytes())
        self.assertEqual(before, self.library.path(entry["revisions"][0]["file"]).read_bytes())
        self.assertTrue(self.library.status()["valid"])

    def test_same_source_is_idempotent(self):
        self.first()
        before = self.library.catalog_path.read_bytes()
        result = self.register()
        self.assertFalse(result["changed"])
        self.assertEqual(result["revisions"], 1)
        self.assertEqual(before, self.library.catalog_path.read_bytes())

    def test_update_keeps_previous_revision_and_stable_current_name(self):
        entry = self.first()
        update = self.folder / "new_filename.aseprite"
        update.write_bytes(b"second source")
        result = self.register(update)
        updated = self.library.load()["motions"]["attack_1"]
        self.assertEqual(result["revisions"], 2)
        self.assertEqual(updated["current"], entry["current"])
        self.assertEqual(self.library.path(entry["revisions"][0]["file"]).read_bytes(), b"first source")
        self.assertEqual(self.library.path(updated["current"]).read_bytes(), b"second source")

    def test_unregistered_local_edits_are_archived_before_a_different_update(self):
        entry = self.first()
        self.library.path(entry["current"]).write_bytes(b"artist working copy")
        self.source.write_bytes(b"new submitted file")
        self.register()
        entry = self.library.load()["motions"]["attack_1"]
        self.assertEqual(len(entry["revisions"]), 3)
        working = next(r for r in entry["revisions"] if r["kind"] == "saved-working-copy")
        self.assertEqual(self.library.path(working["file"]).read_bytes(), b"artist working copy")
        self.assertEqual(self.library.path(entry["current"]).read_bytes(), b"new submitted file")

    def test_registering_current_itself_keeps_previous_version(self):
        entry = self.first()
        current = self.library.path(entry["current"])
        current.write_bytes(b"edited Current")
        self.register(current)
        entry = self.library.load()["motions"]["attack_1"]
        self.assertEqual(len(entry["revisions"]), 2)
        self.assertEqual(entry["revisions"][-1]["kind"], "submission")
        self.assertEqual(current.read_bytes(), b"edited Current")

    def test_a_later_edit_during_registration_is_not_overwritten(self):
        entry = self.first()
        current = self.library.path(entry["current"])
        def concurrent_edit(data):
            current.write_bytes(b"newer artist edit")
            return fake_inspect(data)
        self.library.inspector = concurrent_edit
        self.source.write_bytes(b"incoming revision")
        with self.assertRaisesRegex(LibraryError, "Current changed during registration"):
            self.register()
        self.assertEqual(current.read_bytes(), b"newer artist edit")
        self.assertEqual(self.library.load()["motions"]["attack_1"]["current_sha256"], digest(b"first source"))

    def test_missing_current_can_be_restored_from_history(self):
        entry = self.first()
        current = self.library.path(entry["current"])
        current.unlink()
        self.assertFalse(self.library.status()["valid"])
        self.register(self.library.path(entry["revisions"][0]["file"]))
        self.assertEqual(current.read_bytes(), b"first source")

    def test_modified_history_blocks_updates_instead_of_losing_an_original(self):
        entry = self.first()
        self.library.path(entry["revisions"][0]["file"]).write_bytes(b"corruption")
        self.source.write_bytes(b"new source")
        with self.assertRaisesRegex(LibraryError, "immutable revision"):
            self.register()
        self.assertEqual(self.library.path(entry["current"]).read_bytes(), b"first source")
        self.assertFalse(self.library.status()["valid"])

    def test_status_detects_current_edits_without_modifying_files(self):
        entry = self.first()
        current = self.library.path(entry["current"])
        current.write_bytes(b"unfinished drawing")
        before = self.library.catalog_path.read_bytes()
        result = self.library.status()
        self.assertFalse(result["valid"])
        self.assertTrue(result["motions"][0]["current_edited"])
        self.assertEqual(current.read_bytes(), b"unfinished drawing")
        self.assertEqual(self.library.catalog_path.read_bytes(), before)

    def test_new_source_marks_game_usage_stale_and_confirmation_clears_it(self):
        self.first()
        reference = self.folder / "GameReference.json"
        reference.write_text("{}", encoding="utf-8")
        evidence = self.folder / "validation.md"
        evidence.write_text("verified fixture", encoding="utf-8")
        relative = reference.relative_to(ROOT).as_posix()
        proof = evidence.relative_to(ROOT).as_posix()
        self.library.record_use("attack_1", "video", "VerticalSlash", [relative], proof)
        self.source.write_bytes(b"updated source")
        self.register()
        self.assertEqual(self.library.status()["motions"][0]["needs_game_update"], ["video"])
        self.assertEqual(reference.read_text(encoding="utf-8"), "{}")
        self.library.record_use("attack_1", "video", "VerticalSlash", [relative], proof)
        self.assertEqual(self.library.status()["motions"][0]["needs_game_update"], [])

    def test_usage_confirmation_requires_existing_evidence(self):
        self.first()
        with self.assertRaisesRegex(LibraryError, "Missing workspace reference"):
            self.library.record_use("attack_1", "video", "VerticalSlash", ["missing-file"], "missing-evidence")

    def test_removing_game_evidence_is_visible_in_status(self):
        self.first()
        proof = self.folder / "proof.md"
        proof.write_text("checked", encoding="utf-8")
        relative = proof.relative_to(ROOT).as_posix()
        self.library.record_use("attack_1", "video", "VerticalSlash", [relative], relative)
        proof.unlink()
        self.assertFalse(self.library.status()["valid"])

    def test_another_motion_cannot_replace_the_same_filename(self):
        entry = self.first()
        before = self.library.catalog_path.read_bytes()
        with self.assertRaisesRegex(LibraryError, "filename is already assigned"):
            self.library.register("attack_2", self.source, name="攻撃2")
        self.assertEqual(self.library.catalog_path.read_bytes(), before)
        self.assertEqual(self.library.path(entry["current"]).read_bytes(), b"first source")

    def test_unknown_current_file_is_not_overwritten(self):
        current = self.library.root / "Current" / self.source.name
        current.parent.mkdir(parents=True)
        current.write_bytes(b"unregistered original")
        with self.assertRaisesRegex(LibraryError, "unregistered file"):
            self.first()
        self.assertEqual(current.read_bytes(), b"unregistered original")

    def test_invalid_ids_and_escaping_paths_are_rejected(self):
        for motion_id in ["../attack", "Attack 1", "C:/outside", ""]:
            with self.assertRaises(LibraryError):
                self.library.register(motion_id, self.source, name="invalid")
        with self.assertRaises(LibraryError):
            self.library.path("../../outside.aseprite")

    def test_lock_prevents_a_second_writer(self):
        with self.library.writing():
            with self.assertRaisesRegex(LibraryError, "Another registration"):
                self.first()
        self.assertFalse((self.library.root / ".catalog.lock").exists())

    def test_missing_catalog_is_not_reported_valid(self):
        self.assertFalse(self.library.status()["valid"])

    def test_native_inspection_rejects_non_aseprite_files(self):
        with self.assertRaisesRegex(LibraryError, "Not an Aseprite"):
            inspect_document(b"not a sprite")

    @unittest.skipUnless(Path(DEFAULT_ASEPRITE).is_file(), "Aseprite is required for native inspection")
    def test_actual_counter_source_keeps_sample_visibility_layers_and_native_timing(self):
        source = ROOT / "Assets/Art/Player/VideoSubmitted/20261018/Player_counter_kick.aseprite"
        original = source.read_bytes()
        library = PlayerSourceLibrary(self.folder / "native-library")
        library.register("counter_kick", source, name="反撃キック", status="provisional")
        entry = library.load()["motions"]["counter_kick"]
        metadata = entry["revisions"][0]["metadata"]
        layers = {l["name"]: l for l in metadata["layers"]}
        self.assertTrue(layers["Sample"]["visible"])
        self.assertFalse(layers["Body"]["visible"])
        self.assertEqual((metadata["width"], metadata["height"]), (128, 128))
        self.assertEqual([f["duration_ms"] for f in metadata["frames"]], [80]*3+[150]*4+[80]*8)
        self.assertEqual(original, library.path(entry["current"]).read_bytes())
        self.assertEqual(original, source.read_bytes())

    @unittest.skipUnless(Path(DEFAULT_ASEPRITE).is_file(), "Aseprite is required for native updates")
    def test_actual_aseprite_update_refreshes_timing_and_retains_previous_source(self):
        original = (ROOT / "Assets/Art/Player/VideoSubmitted/20261018/Player_counter_kick.aseprite").read_bytes()
        source, update = self.folder / "kick.aseprite", self.folder / "kick_updated.aseprite"
        source.write_bytes(original)
        script = self.folder / "change-duration.lua"
        script.write_text("local s=assert(app.open(app.params['input']));s.frames[4].duration=.2;s:saveAs(app.params['output']);s:close()", encoding="utf-8")
        subprocess.run([DEFAULT_ASEPRITE, "--batch", "--script-param", "input=" + str(source),
                        "--script-param", "output=" + str(update), "--script", str(script)],
                       check=True, timeout=90, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        library = PlayerSourceLibrary(self.folder / "native-update")
        library.register("counter_kick", source, name="反撃キック")
        old = library.load()["motions"]["counter_kick"]
        library.register("counter_kick", update)
        entry = library.load()["motions"]["counter_kick"]
        self.assertEqual(len(entry["revisions"]), 2)
        self.assertEqual(entry["revisions"][-1]["metadata"]["frames"][3]["duration_ms"], 200)
        self.assertEqual(entry["current"], old["current"])
        self.assertEqual(library.path(entry["current"]).read_bytes(), update.read_bytes())
        self.assertEqual(library.path(old["revisions"][0]["file"]).read_bytes(), original)
        self.assertEqual(source.read_bytes(), original)


if __name__ == "__main__":
    unittest.main()

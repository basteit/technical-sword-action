"""Export the registered counter pair without rebuilding other artwork or clips."""
from pathlib import Path
import argparse
import json
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Assets/Art/Player/VideoSubmitted"
EFFECTS = ROOT / "Assets/Art/Video/Submitted/20261024"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--aseprite", default="C:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe")
    args = parser.parse_args()
    scratch = ROOT / ".uloop/outputs/counter-split-export"
    scratch.mkdir(parents=True, exist_ok=True)
    # Keep isolated export output for inspection; never clean the shared checkout.
    work = Path(tempfile.mkdtemp(prefix="export-", dir=scratch))
    body, effect = work / "player", work / "effect"
    (body / "EyeMasks").mkdir(parents=True)
    effect.mkdir()
    sources = ROOT / "ArtSource/Player/Current"
    subprocess.run([
        args.aseprite, "--batch", "--script-param", f"input={sources / 'Player_counter_kick.aseprite'}",
        "--script-param", f"effect={sources / 'Player_counter_kick_effect.aseprite'}",
        "--script-param", f"out={body}", "--script-param", f"fxout={effect}",
        "--script", str(ROOT / "Tools/Art/export-counter-split-submissions.lua"),
    ], check=True)
    # The Lua export verifies every reference cel before producing output.
    exported = read(body / "timing-manifest.json")["actions"]
    manifest = read(PLAYER / "timing-manifest.json")
    exported_body = next(a for a in exported if a["name"] == "CounterKick")
    exported_effect = next(a for a in exported if a["name"] == "CounterKickEffect")
    assert exported_body["frames"] == exported_effect["frames"]
    for action in manifest["actions"]:
        if action["name"] == "CounterKick":
            action.update(exported_body)
    old_eyes = read(PLAYER / "eye-mask-manifest.json")
    new_eyes = read(body / "eye-mask-manifest.json")
    old_eyes["frames"] = [f for f in old_eyes["frames"] if not f["file"].startswith("CounterKick_")] + new_eyes["frames"]
    files = [(body / f"CounterKick_{i:02}.png", PLAYER / f"CounterKick_{i:02}.png") for i in range(1, 16)]
    files += [(body / "EyeMasks" / f"CounterKick_{i:02}.png", PLAYER / "EyeMasks" / f"CounterKick_{i:02}.png") for i in range(1, 16)]
    files += [(effect / f"CounterKickEffect_{i:02}.png", EFFECTS / f"CounterKickEffect_{i:02}.png") for i in range(1, 16)]
    for source, _ in files:
        if not source.is_file():
            raise RuntimeError(f"Incomplete export: {source}")
    EFFECTS.mkdir(parents=True, exist_ok=True)
    for source, destination in files:
        shutil.copyfile(source, destination)  # Preserve the existing .meta/GUID.
    write(PLAYER / "timing-manifest.json", manifest)
    write(PLAYER / "eye-mask-manifest.json", old_eyes)
    write(EFFECTS / "timing-manifest.json", {"actions": [exported_effect]})
    shutil.copyfile(body / "submission-validation.json", EFFECTS / "submission-validation.json")
    print("Exported 15 body, 15 eye masks and 15 effect frames. Apply Split Counter Artwork in Unity next.")


if __name__ == "__main__":
    main()

"""Exercise the real Aseprite exporter with legacy/split tags and expanded canvases.

Fixtures are generated in .uloop/outputs; no production art is opened or changed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--aseprite", default=r"C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    out = root / ".uloop/outputs" / ("player-clean-export-" + uuid.uuid4().hex[:8])
    out.mkdir(parents=True)
    builder = out / "fixtures.lua"
    builder.write_text("""
local out=assert(app.params['out'])
local function fixture(size,split,empty)
  local s=Sprite(size,size,ColorMode.RGB);s.layers[1].name='CleanUp - body'
  local index=0;local tags={}
  local function action(name,durations)
    local first=index+1
    for _,duration in ipairs(durations) do
      index=index+1;if index>1 then s:newEmptyFrame(index) end
      s.frames[index].duration=duration/60
      local im=Image(size,size,ColorMode.RGB)
      if name~=empty then
        im:drawPixel(32,size-38,app.pixelColor.rgba(62,79,183,255))
        im:drawPixel(33,size-37,app.pixelColor.rgba(243,245,251,255))
      end
      s:newCel(s.layers[1],index,im,Point(0,0))
    end
    tags[#tags+1]={name,first,index}
  end
  action('VerticalSlash',{24});action('HorizontalSlash',{22})
  action('AirRoundhouse',{18,6,4,8})
  for _,name in ipairs({'Recovery_VerticalSlash','Recovery_HorizontalSlash','Recovery_AirRoundhouse'}) do action(name,{12}) end
  if split then
    action('ParryReady',{2,3,14});action('ParrySuccessLight',{2,2,2,2,2})
    action('ParrySuccessHeavy',{3,3,4});action('Recovery_ParrySuccessLight',{4,4,4})
  else action('ParryDeflect',{3,3,2,2,4,5});action('Recovery_ParryDeflect',{3,4,4,4}) end
  for _,tag in ipairs(tags) do s:newTag(tag[2],tag[3]).name=tag[1] end
  s:saveAs(out..'/'..(empty and 'empty' or split and 'split' or 'legacy')..size..'.aseprite');s:close()
end
fixture(64,false);fixture(64,true);fixture(128,true);fixture(64,true,'ParrySuccessLight')
""", encoding="utf-8")

    def run(script: Path, parameters: dict[str, str]) -> subprocess.CompletedProcess:
        command = [args.aseprite, "-b"]
        for key, value in parameters.items():
            command += ["--script-param", f"{key}={value}"]
        command += ["--script", str(script)]
        return subprocess.run(command, capture_output=True, text=True, timeout=90,
                              creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))

    made = run(builder, {"out": out.as_posix()})
    assert made.returncode == 0, made.stdout + made.stderr
    exporter = root / "Tools/Art/export-player-clean.lua"
    results = []

    def check(name: str, source_name: str, parameters: dict[str, str], *, error: str | None = None) -> None:
        source = out / source_name
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        target = out / name
        (target / "EyeMasks").mkdir(parents=True)
        result = run(exporter, {"input": source.as_posix(), "out": target.as_posix(), **parameters})
        assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash, "Exporter changed its source"
        manifest_file = target / "timing-manifest.json"
        if error:
            assert not manifest_file.exists(), "Invalid handoff was accepted"
            assert error in result.stdout + result.stderr, result.stdout + result.stderr
        else:
            assert result.returncode == 0 and manifest_file.exists(), result.stdout + result.stderr
            manifest = json.loads(manifest_file.read_text())
            expected_size = 128 if "128" in source_name else 64
            assert manifest["canvas"] == expected_size and manifest["pivot"] == [32, 38]
            for action in manifest["actions"]:
                assert action["canvasWidth"] == expected_size and action["canvasHeight"] == expected_size
                assert action["pivotX"] == 32 and action["pivotY"] == 38
                assert sum(frame["frames"] for frame in action["frames"]) == action["totalFrames"]
                for index in range(1, action["count"] + 1):
                    file = f'{action["name"]}_{index:02}.png'
                    with Image.open(target / file) as image:
                        assert image.size == (expected_size, expected_size)
                        assert image.getpixel((32, expected_size - 38)) == (62, 79, 183, 255)
                    with Image.open(target / "EyeMasks" / file) as mask:
                        assert mask.size == (expected_size, expected_size)
                        assert mask.getpixel((32, expected_size - 38)) == (255, 255, 255, 255)
            if "split" in source_name:
                actions = {a["name"]: a for a in manifest["actions"]}
                assert actions["ParryReady"]["totalFrames"] == 19
                assert actions["ParrySuccessLight"]["totalFrames"] == 10
                assert "ParryDeflect" not in actions
        results.append({"case": name, "passed": True, "sourceUnchanged": True})

    check("legacy-full", "legacy64.aseprite", {})
    check("split-partial", "split64.aseprite", {"only": "ParryReady,ParrySuccessLight,ParrySuccessHeavy"})
    check("expanded-split-full", "split128.aseprite", {"pivot-x": "32", "pivot-y": "38"})
    check("expanded-needs-pivot", "split128.aseprite", {}, error="Expanded canvas requires")
    check("unknown-tag", "split64.aseprite", {"only": "NotAnAction"}, error="Unknown cleanup Action tag")
    check("empty-required", "empty64.aseprite", {"only": "ParrySuccessLight"}, error="Required cleanup frame is empty")
    check("missing-required", "split64.aseprite", {"only": "Recovery_ParrySuccessHeavy"}, error="Missing required Action tag")
    check("invalid-pivot", "split128.aseprite", {"pivot-x": "129", "pivot-y": "38"}, error="Pivot is outside the canvas")
    report = {"passed": len(results), "failed": 0, "cases": results, "artifacts": str(out)}
    (out / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

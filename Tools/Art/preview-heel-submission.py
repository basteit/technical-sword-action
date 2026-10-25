"""Read-only checks and nearest-pixel previews for the provisional attack / heel drop."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SUB = ROOT / 'Assets/Art/Player/VideoSubmitted'
HEEL = ROOT / 'Assets/Art/Player/VideoHeelDrop'
OUT = ROOT / 'docs/video/rough-previews'
OUT.mkdir(parents=True, exist_ok=True)


def preview(im, scale):
    bg = Image.new('RGBA', im.size, '#e7ebf1')
    bg.alpha_composite(im.convert('RGBA'))
    return bg.convert('RGB').resize((im.width*scale, im.height*scale), Image.Resampling.NEAREST)


submission = json.loads((SUB / 'timing-manifest.json').read_text())['actions'][0]
assert [f['frames'] for f in submission['frames']] == [4,4,5,4,4,4,4,5,4]
assert submission['totalFrames'] == 38 and submission['hitFrame'] == 17
images = [preview(Image.open(SUB / f'VerticalSlash_{i:02}.png'), 3) for i in range(1,10)]
images[0].save(OUT / 'SubmittedAttack1.gif', save_all=True, append_images=images[1:], duration=70, loop=0, disposal=2)

actions = {a['name']: a for a in json.loads((HEEL / 'timing-manifest.json').read_text())['actions']}
assert actions['AirRoundhouse']['totalFrames'] == 36
assert actions['Recovery_AirRoundhouse']['totalFrames'] == 33
images, durations = [], []
for name in ['AirRoundhouse', 'Recovery_AirRoundhouse']:
    for i, frame in enumerate(actions[name]['frames'],1):
        if name == 'AirRoundhouse' and frame['start'] < 18:
            continue
        im = Image.open(HEEL / f'{name}_{i:02}.png')
        images.append(preview(im,4))
        seconds = frame['frames']/60
        if name == 'AirRoundhouse' and 18 <= frame['start'] < 24:
            seconds /= .15
        if name == 'AirRoundhouse' and frame['start'] == 28:
            seconds += 8/60
        durations.append(round(seconds*1000))
images[0].save(OUT / 'HeelDrop-slow.gif', save_all=True, append_images=images[1:], duration=durations, loop=0, disposal=2)

sheet = Image.new('RGB',(5*192,2*220),'#e7ebf1')
draw = ImageDraw.Draw(sheet)
for n,i in enumerate([6,9,12,13,14,15,16,17,18,19]):
    x,y=(n%5)*192,(n//5)*220
    sheet.paste(preview(Image.open(HEEL / f'AirRoundhouse_{i:02}.png'),3),(x,y))
    draw.text((x+4,y+195),f"Heel {i} / {actions['AirRoundhouse']['frames'][i-1]['start']}F",fill='#222034')
sheet.save(OUT / 'heel-drop-contact.png')

masks=json.loads((HEEL / 'eye-mask-manifest.json').read_text())['frames']
for item in masks:
    mask=Image.open(HEEL / 'EyeMasks' / (item['file']+'.png')).convert('RGBA')
    assert sum(p[3]>0 for p in mask.get_flattened_data())==item['pixels']
for i in range(6,12):
    assert next(m['pixels'] for m in masks if m['file']==f'AirRoundhouse_{i:02}')>0
hashes={}
for path in [SUB/'20261016/Player_attack_1.aseprite', ROOT/'Assets/Art/Player/Player_idle.aseprite', ROOT/'Assets/Art/Player/Player_walk.aseprite', ROOT/'Assets/Art/Player/VideoRough/Player_actions_rough.aseprite']:
    hashes[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest().upper()
report={'submission':submission,'heelEditable':json.loads((HEEL/'authoring-validation.json').read_text()),'eyeMasksChecked':len(masks),'allSlowPosesHaveEyeMasks':True,'hashes':hashes}
(OUT/'heel-submission-art-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))

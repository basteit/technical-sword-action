"""Inspect Aseprite exports and build pixel-nearest motion previews (requires Pillow)."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Player/VideoRough'
OUT = ROOT / 'docs/video/rough-previews'
OUT.mkdir(parents=True, exist_ok=True)
actions = json.loads((ART / 'timing-manifest.json').read_text())['actions']
lookup = {a['name']: a for a in actions}


def source(name, index):
    return Image.open(ART / f'{name}_{index:02d}.png').convert('RGBA')


def canvas(im, scale=4):
    bg = Image.new('RGBA', (64, 64), '#edf0f5')
    bg.alpha_composite(im)
    return bg.convert('RGB').resize((64*scale, 64*scale), Image.Resampling.NEAREST)


def pixels(im):
    return im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata()


for action in actions:
    parts = [action]
    if 'Recovery_' + action['name'] in lookup:
        parts.append(lookup['Recovery_' + action['name']])
    images, durations = [], []
    for part in parts:
        cut = (action.get('recoveryFrame') or action['totalFrames']) if part is action else part['totalFrames']
        for i, frame in enumerate(part['frames']):
            if frame['start'] >= cut:
                continue
            images.append(canvas(source(part['name'], i+1)))
            durations.append(round(min(frame['frames'], cut-frame['start'])/60*1000))
    images[0].save(OUT / f'{action["name"]}.gif', save_all=True, append_images=images[1:], duration=durations, loop=0, disposal=2)

# Readable in isolation: inverted coil, fast downward tumble, contact and recovery.
images, durations = [], []
for part in [lookup['AirRoundhouse'], lookup['Recovery_AirRoundhouse']]:
    for i, frame in enumerate(part['frames']):
        if part['name'] == 'AirRoundhouse' and frame['start'] < 18:
            continue
        seconds = frame['frames']/60
        if part['name'] == 'AirRoundhouse':
            if 18 <= frame['start'] < 24:
                seconds /= .15
            if frame['start'] == 28:
                seconds += 8/60
        images.append(canvas(source(part['name'], i+1)))
        durations.append(round(seconds*1000))
images += [canvas(source('IdleRough', 1))]
durations += [400]
images[0].save(OUT / 'AirRoundhouse-slow.gif', save_all=True, append_images=images[1:], duration=durations, loop=0, disposal=2)

rows = [('VerticalSlash', [1,2,3,4,5,8]), ('HorizontalSlash', [1,2,3,4,6,8]),
        ('ParryDeflect', [1,2,3,4,5,6]), ('AirRoundhouse', [6,7,8,9,10,11])]
sheet = Image.new('RGB', (6*192, len(rows)*216), '#edf0f5')
draw = ImageDraw.Draw(sheet)
for y, (name, indices) in enumerate(rows):
    for x, index in enumerate(indices):
        sheet.paste(canvas(source(name, index), 3), (x*192, y*216))
        start = lookup[name]['frames'][index-1]['start']
        draw.text((x*192+4, y*216+195), f'{name} {index} / {start}F', fill='#222034')
sheet.save(OUT / 'redrawn-action-contact.png')

# Source provenance, masks, empty canvas margins and the neutral recovery endpoint.
eyes = json.loads((ART / 'eye-mask-manifest.json').read_text())['frames']
for record in eyes:
    im = Image.open(ART / (record['file']+'.png')).convert('RGBA')
    mask = Image.open(ART / 'EyeMasks' / (record['file']+'.png')).convert('RGBA')
    count = 0
    for p, e in zip(pixels(im), pixels(mask)):
        if e[3]:
            assert p[:3] in [(41,58,157), (62,79,183), (91,110,225)], (record['file'], p)
            assert e == (255,255,255,255)
            count += 1
    assert count == record['pixels'], record
assert source('Recovery_AirRoundhouse', 7).tobytes() == source('IdleRough', 1).tobytes()
floor = Image.open(ROOT / 'Assets/Art/Video/CaptureTrainingGrid.png').convert('RGBA')
assert set(pixels(floor.crop((0,156,384,216)))) == {(164,179,199,255)}
print(json.dumps({'eyeMasksChecked': len(eyes), 'roughNeutralMatchesRecovery': True,
                  'flatFloorColours': 1, 'actionPreviews': len(actions), 'slowKickPreview': True}))

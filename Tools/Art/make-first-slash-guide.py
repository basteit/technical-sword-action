from pathlib import Path
import csv
from PIL import Image,ImageDraw,ImageFont
root=Path.cwd();art=root/'Assets/Art/Player/VideoRough';out=root/'docs/video/rough-previews'
rows=list(csv.DictReader((art/'timing.csv').open(encoding='utf-8-sig')))
keys=[r for r in rows if r['Action'] in ['VerticalSlash','Recovery_VerticalSlash']]
s=Image.new('RGB',(1280,840),'#eef2f7');d=ImageDraw.Draw(s);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
for i,r in enumerate(keys):
 x=(i%4)*320;y=(i//4)*280;name=r['Action'];local=int(r['LocalFrame']);start=int(r['StartGameFrame']);dur=int(r['GameFrames'])
 im=Image.open(art/f'{name}_{local:02d}.png').convert('RGBA');s.paste(im.resize((256,256),Image.Resampling.NEAREST),(x+32,y),im.resize((256,256),Image.Resampling.NEAREST))
 short='Slash' if name=='VerticalSlash' else 'Recovery';d.text((x+10,y+258),f'{short} {local} / doc {r["DocumentFrame"]} / {start}-{start+dur-1}F',font=font,fill='#222034')
s.save(out/'first-slash-all-frames.png')
assert len(keys)==12
print('First slash guide: 8 action + 4 recovery poses')

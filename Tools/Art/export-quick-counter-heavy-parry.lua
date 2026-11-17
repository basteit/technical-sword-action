-- Export received roughs; only hide the verified black guide in memory. Never save the originals.
local root=assert(app.params['root'])
local out=assert(app.params['out'])
local motions={}
for _,item in ipairs{{id='parry_heavy',prefix='ParryHeavy',count=8},{id='counter_quick',prefix='CounterQuick',count=6}} do
  local source='ArtSource/Player/Current/Player_'..item.id..'.aseprite'
  local sprite=assert(app.open(root..'/'..source))
  assert(sprite.width==128 and sprite.height==128 and #sprite.frames==item.count,'Unexpected received rough; inspect first')
  local guide=sprite.layers[1];assert(guide.name=='Layer 1','Expected the separately authored guide')
  for _,cel in ipairs(guide.cels) do for pixel in cel.image:pixels() do
    local value=pixel();assert(app.pixelColor.rgbaA(value)==0 or value==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork')
  end end
  guide.isVisible=false
  local frames={}
  for i,frame in ipairs(sprite.frames) do
    local image=Image(sprite.spec);image:drawSprite(sprite,i)
    local file=item.prefix..'_'..string.format('%02d',i)..'.png'
    image:saveAs(out..'/'..file)
    frames[#frames+1]={file=file,sourceFrame=i,durationMs=math.floor(frame.duration*1000+.5)}
  end
  motions[#motions+1]={id=item.id,source=source,guideHiddenForExport=true,frames=frames}
  sprite:close()
end
local file=assert(io.open(out..'/export-manifest.json','w'))
file:write(json.encode{canvasWidth=128,canvasHeight=128,pixelsPerUnit=32,pivotX=32,pivotY=38,motions=motions})
file:close()

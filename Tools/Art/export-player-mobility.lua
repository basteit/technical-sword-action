-- Export the received mobility art without editing Current/History or changing layer visibility there.
local root=assert(app.params['root'])
local out=assert(app.params['out'])
local review=app.params['review'] or out
local records={}
for _,motion in ipairs{{id='jump',file='Player_jamp',count=7},{id='dash',file='Player_dash',count=3}} do
  local s=assert(app.open(root..'/ArtSource/Player/Current/'..motion.file..'.aseprite'))
  assert(s.width==64 and s.height==64 and #s.frames==motion.count,'Unexpected mobility revision')
  local guide=nil
  for _,layer in ipairs(s.layers) do if layer.name=='Layer 1' then guide=layer end end
  assert(guide,'Missing guide layer; inspect before export')
  for _,cel in ipairs(guide.cels) do
    for p in cel.image:pixels() do
      local c=p()
      assert(app.pixelColor.rgbaA(c)==0 or c==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork; inspect before hiding it')
    end
  end
  guide.isVisible=false -- In-memory output only. Never save the source document.
  local frames={};local sheet=Image(64*motion.count,64,ColorMode.RGB)
  for i,f in ipairs(s.frames) do
    local im=Image(s.spec);im:drawSprite(s,i)
    local name=motion.id=='jump' and 'Jump' or 'Dash'
    local file=name..'_'..string.format('%02d',i)..'.png'
    im:saveAs(out..'/'..file);sheet:drawImage(im,Point((i-1)*64,0))
    frames[#frames+1]={file=file,sourceFrame=i,durationMs=math.floor(f.duration*1000+.5)}
  end
  sheet:saveAs(review..'/'..motion.id..'-review.png')
  records[#records+1]={id=motion.id,source='ArtSource/Player/Current/'..motion.file..'.aseprite',guideHiddenForExport=true,frames=frames}
  s:close()
end
local f=assert(io.open(out..'/export-manifest.json','w'))
f:write(json.encode{canvasWidth=64,canvasHeight=64,pixelsPerUnit=32,pivotX=32,pivotY=38,motions=records})
f:close()

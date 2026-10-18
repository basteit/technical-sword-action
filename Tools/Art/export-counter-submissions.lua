-- Aseprite owns compositing, layer visibility and the editable review copies.
-- Original downloads are never saved over. Native timing stays in each review.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out']);local kind=assert(app.params['kind'])
local transfer=kind=='transfer';assert(transfer or kind=='kick')
local name=transfer and 'CounterTransfer' or 'CounterKick'
local count=transfer and 8 or 15;local size=transfer and 64 or 128
assert(s.width==size and s.height==size and #s.frames==count,'Inspect this revision before exporting')
local guide=s.layers[1];assert(guide.name=='Layer 1')
for _,cel in ipairs(guide.cels) do for p in cel.image:pixels() do
  assert(app.pixelColor.rgbaA(p())==0 or p()==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork')
end end
guide.isVisible=false
local removedMarkers=0
if not transfer then
  local sample=s.layers[#s.layers];assert(sample.name=='Sample' and sample.isVisible and sample.opacity==255)
  -- The seven isolated reference dots are outside the silhouette, not a body part.
  for i=1,7 do local c=assert(sample:cel(i));local im=c.image:clone()
    local x,y=91-c.position.x,44-c.position.y
    assert(im:getPixel(x,y)==app.pixelColor.rgba(34,32,52,255),'Reference marker moved')
    for dy=-1,1 do for dx=-1,1 do if dx~=0 or dy~=0 then
      local px,py=x+dx,y+dy
      assert(px<0 or py<0 or px>=im.width or py>=im.height or app.pixelColor.rgbaA(im:getPixel(px,py))==0,'Marker touches artwork')
    end end end
    im:drawPixel(x,y,app.pixelColor.rgba(0,0,0,0));c.image=im;removedMarkers=removedMarkers+1
  end
end
-- The four tucked poses ease toward the extension; native source durations stay untouched.
local ticks=transfer and {1,1,2,2,2,1,1,2} or {2,2,2,3,2,2,1,1,1,2,2,2,2,2,2}
local frames,eyes,native={},{},{};local start=0;local sheet=Image(size*count,size,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  local im=Image(s.spec);im:drawSprite(s,i)
  if not transfer and i==9 then
    assert(im:getPixel(73,86)==app.pixelColor.rgba(34,32,52,255),'The drawn contact moved; inspect the toe before changing gameplay metadata')
  end
  local file=name..'_'..string.format('%02d',i)
  im:saveAs(out..'/'..file..'.png');sheet:drawImage(im,Point((i-1)*size,0))
  local mask=Image(s.spec);local pixels=0
  if transfer then
    -- Use the authored Eye layer even when its preview opacity is 50 percent.
    local eye
    for _,l in ipairs(s.layers) do if l.name=='Eye' then eye=l end end
    local cel=eye and eye:cel(i)
    if cel then for p in cel.image:pixels() do if app.pixelColor.rgbaA(p())>0 then
      mask:drawPixel(p.x+cel.position.x,p.y+cel.position.y,app.pixelColor.rgba(255,255,255,255));pixels=pixels+1
    end end end
  end
  mask:saveAs(out..'/EyeMasks/'..file..'.png');eyes[#eyes+1]={file=file,pixels=pixels}
  frames[#frames+1]={frames=ticks[i],start=start};start=start+ticks[i]
  native[#native+1]={frame=i,durationMs=math.floor(f.duration*1000+.5)}
end
sheet:saveAs(out..'/'..name..'.png')
local tag=s:newTag(1,count);tag.name=name
local review='Player_counter_'..kind..'_preview.aseprite'
s:saveAs(out..'/'..review);s:close()
local check=assert(app.open(out..'/'..review));assert(#check.frames==count and not check.layers[1].isVisible and check.tags[1].name==name)
for i,f in ipairs(check.frames) do assert(math.floor(f.duration*1000+.5)==native[i].durationMs) end
check:close()
local action={name=name,provisional=true,canvasWidth=size,canvasHeight=size,pivotX=32,pivotY=38,count=count,totalFrames=start,frames=frames}
if not transfer then action.chargeFrame=6;action.releaseFrame=14;action.hitFrame=15;action.landingFrame=28;action.recoveryFrame=28 end
local function write(file,value) local f=assert(io.open(out..'/'..file,'w'));f:write(json.encode(value));f:close() end
write('timing-manifest.json',{actions={action}});write('eye-mask-manifest.json',{frames=eyes})
write('submission-validation.json',{sourceUnmodified=true,reviewCopyReopened=true,guideHidden=true,removedIsolatedGuidePixels=removedMarkers,nativeFrames=native,gameFrames=start,kind=kind,silhouetteSampleOnly=not transfer,pivot={x=32,y=38},contact=not transfer and {frame=9,x=73,y=86} or nil})

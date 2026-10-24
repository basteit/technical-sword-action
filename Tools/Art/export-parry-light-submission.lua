-- Import either received light-parry revision without editing its source.
-- Preserve native timing in the review copy; gameplay owns acceptance and hitstop.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out'])
local revised=s.width==128 and s.height==128 and #s.frames==8
assert(revised or (s.width==64 and s.height==64 and #s.frames==7),'Inspect the new light-parry revision first')
local guide=s.layers[1];assert(guide.name=='Layer 1')
for _,cel in ipairs(guide.cels) do for p in cel.image:pixels() do
  assert(app.pixelColor.rgbaA(p())==0 or p()==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork')
end end
guide.isVisible=false
-- Keep ten ticks and start the last cel by tick 8, before the counter may cancel it.
local ticks=revised and {2,1,1,1,1,1,1,2} or {2,1,1,2,1,1,2}
local frames,eyes,native={},{},{};local start=0
local sheet=Image(s.width*#s.frames,s.height,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  local ms=math.floor(f.duration*1000+.5)
  assert(ms==(revised and 80 or 100),'Unexpected native duration')
  local im=Image(s.spec);im:drawSprite(s,i)
  local name='ParrySuccessLight_'..string.format('%02d',i)
  im:saveAs(out..'/'..name..'.png');sheet:drawImage(im,Point((i-1)*s.width,0))
  local mask=Image(s.spec);local count=0
  for y=0,s.height-1 do for x=0,s.width-1 do local c=im:getPixel(x,y)
    local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
    if app.pixelColor.rgbaA(c)>0 and ((r==41 and g==58 and b==157) or (r==62 and g==79 and b==183) or (r==91 and g==110 and b==225)) then
      mask:drawPixel(x,y,app.pixelColor.rgba(255,255,255,255));count=count+1
    end
  end end
  mask:saveAs(out..'/EyeMasks/'..name..'.png');eyes[#eyes+1]={file=name,pixels=count}
  frames[#frames+1]={frames=ticks[i],start=start};native[#native+1]={frame=i,durationMs=ms};start=start+ticks[i]
end
-- The enlarged canvas extends upward/right; the original bottom-origin pivot stays (32,38).
-- The revised authored spark converges at top-origin (77,84), near the outward deflection.
local cx,cy=revised and 77 or 44,revised and 84 or 38
if not revised then
  local contact=Image(s.spec);contact:drawSprite(s,1)
  assert(contact:getPixel(cx,cy)==app.pixelColor.rgba(39,64,219,255),'Blade contact moved; inspect its metadata')
end
sheet:saveAs(out..'/ParrySuccessLight.png')
local count=#s.frames;local width,height=s.width,s.height
local tag=s:newTag(1,count);tag.name='ParrySuccessLight'
s:saveAs(out..'/Player_parry_light_preview.aseprite');s:close()
local check=assert(app.open(out..'/Player_parry_light_preview.aseprite'))
assert(#check.frames==count and check.tags[1].name=='ParrySuccessLight' and not check.layers[1].isVisible)
for _,f in ipairs(check.frames) do assert(math.floor(f.duration*1000+.5)==(revised and 80 or 100)) end
check:close()
local function write(name,value) local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(value));f:close() end
write('timing-manifest.json',{actions={{name='ParrySuccessLight',provisional=true,includesParryEffect=revised,canvasWidth=width,canvasHeight=height,pivotX=32,pivotY=38,count=count,totalFrames=start,hitFrame=0,branchFrame=0,hasParryContact=true,parryContactOffset={x=(cx-32)/32,y=(height-38-cy)/32},frames=frames}}})
write('eye-mask-manifest.json',{frames=eyes})
write('submission-validation.json',{sourceUnmodified=true,reviewCopyReopened=true,guideHidden=true,nativeFrames=native,nativeDurationMs=count*(revised and 80 or 100),gameFrames=start,contactPixel={x=cx,y=cy},authoredParryEffect=revised,acceptanceTimingUnchanged=true})

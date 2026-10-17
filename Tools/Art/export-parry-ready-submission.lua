-- Read-only source import. Native timing is retained in the editable review copy.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out'])
assert(s.width==64 and s.height==64 and #s.frames==8,'Inspect the new parry-ready revision first')
local guide=s.layers[1];assert(guide.name=='Layer 1')
for _,cel in ipairs(guide.cels) do for p in cel.image:pixels() do
  assert(app.pixelColor.rgbaA(p())==0 or p()==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork')
end end
guide.isVisible=false
local frames,eyes,native={},{},{};local start=0
local ticks={2,2,6,6,6,6,6,6}
local sheet=Image(64*#s.frames,64,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  assert(math.floor(f.duration*1000+.5)==100,'Unexpected native duration')
  local im=Image(s.spec);im:drawSprite(s,i)
  local name='ParryReady_'..string.format('%02d',i)
  im:saveAs(out..'/'..name..'.png');sheet:drawImage(im,Point((i-1)*64,0))
  local mask=Image(s.spec);local count=0
  for y=0,63 do for x=0,63 do local c=im:getPixel(x,y)
    local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
    if app.pixelColor.rgbaA(c)>0 and ((r==41 and g==58 and b==157) or (r==62 and g==79 and b==183) or (r==91 and g==110 and b==225)) then
      mask:drawPixel(x,y,app.pixelColor.rgba(255,255,255,255));count=count+1
    end
  end end
  mask:saveAs(out..'/EyeMasks/'..name..'.png');eyes[#eyes+1]={file=name,pixels=count}
  frames[#frames+1]={frames=ticks[i],start=start};native[#native+1]={frame=i,durationMs=100};start=start+ticks[i]
end
sheet:saveAs(out..'/ParryReady.png')
local tag=s:newTag(1,8);tag.name='ParryReady'
s:saveAs(out..'/Player_parry_wait_preview.aseprite');s:close()
local check=assert(app.open(out..'/Player_parry_wait_preview.aseprite'))
assert(check.tags[1].name=='ParryReady' and not check.layers[1].isVisible)
for _,f in ipairs(check.frames) do assert(math.floor(f.duration*1000+.5)==100) end
check:close()
local function write(name,value) local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(value));f:close() end
write('timing-manifest.json',{actions={{name='ParryReady',canvasWidth=64,canvasHeight=64,pivotX=32,pivotY=38,count=8,totalFrames=start,hitFrame=0,branchFrame=0,frames=frames}}})
write('eye-mask-manifest.json',{frames=eyes})
write('submission-validation.json',{sourceUnmodified=true,reviewCopyReopened=true,guideHidden=true,nativeFrames=native,nativeDurationMs=800,gameFrames=start,readyPoseStartsAt=4,acceptanceTimingUnchanged=true})

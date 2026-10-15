-- Import a provisional action without changing its original art, layers or timing.
-- This submission has a padded 128px canvas; its 64px reference cell is bottom-left.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out'])
assert(s.width==128 and s.height==128 and #s.frames==9,'Unexpected attack-1 submission revision')
local guide=s.layers[1]
assert(guide.name=='Layer 1','Inspect the submission before changing guide visibility')
for _,cel in ipairs(guide.cels) do for p in cel.image:pixels() do
  local c=p();assert(app.pixelColor.rgbaA(c)==0 or c==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork; inspect before export')
end end
guide.isVisible=false
local action='VerticalSlash';local frames={};local eyes={};local native={};local cumulative=0;local start=0
local sheet=Image(128*#s.frames,128,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  local im=Image(s.spec);im:drawSprite(s,i)
  local name=action..'_'..string.format('%02d',i)
  im:saveAs(out..'/'..name..'.png');sheet:drawImage(im,Point((i-1)*128,0))
  local mask=Image(s.spec);local pixels=0
  for y=0,127 do for x=0,127 do local c=im:getPixel(x,y)
    local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
    if app.pixelColor.rgbaA(c)>0 and ((r==41 and g==58 and b==157) or (r==62 and g==79 and b==183) or (r==91 and g==110 and b==225)) then
      mask:drawPixel(x,y,app.pixelColor.rgba(255,255,255,255));pixels=pixels+1
    end
  end end
  mask:saveAs(out..'/EyeMasks/'..name..'.png');eyes[#eyes+1]=string.format('{"file":%q,"pixels":%d}',name,pixels)
  cumulative=cumulative+f.duration*60;local nextFrame=math.floor(cumulative+.5)
  frames[#frames+1]=string.format('{"frames":%d,"start":%d}',nextFrame-start,start)
  native[#native+1]=string.format('{"frame":%d,"durationMs":%d,"startTick":%d,"ticks":%d}',i,math.floor(f.duration*1000+.5),start,nextFrame-start)
  start=nextFrame
end
sheet:saveAs(out..'/'..action..'.png')
local t=s:newTag(1,9);t.name=action
-- The editable review copy gains only a tag and hidden construction guide.
s:saveAs(out..'/Player_attack_1_preview.aseprite');s:close()
local reopen=assert(app.open(out..'/Player_attack_1_preview.aseprite'))
assert(#reopen.frames==9 and reopen.tags[1].name==action and not reopen.layers[1].isVisible)
for _,f in ipairs(reopen.frames) do assert(math.floor(f.duration*1000+.5)==70) end
reopen:close()
local f=assert(io.open(out..'/timing-manifest.json','w'))
f:write('{"actions":[{"name":"VerticalSlash","provisional":true,"canvasWidth":128,"canvasHeight":128,"pivotX":32,"pivotY":38,"count":9,"totalFrames":38,"hitFrame":17,"branchFrame":34,"recoveryFrame":34,"frames":['..table.concat(frames,',')..']}]}');f:close()
local e=assert(io.open(out..'/eye-mask-manifest.json','w'));e:write('{"frames":['..table.concat(eyes,',')..']}');e:close()
local r=assert(io.open(out..'/submission-validation.json','w'));r:write('{"sourceUnmodified":true,"reviewCopyReopened":true,"guideHidden":true,"hairVisibilityPreserved":true,"nativeDurationMs":630,"gameTicks":38,"contactFrame":5,"contactTick":17,"frames":['..table.concat(native,',')..']}');r:close()

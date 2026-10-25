-- Import a provisional action without changing its original art, layers or timing.
-- This submission has a padded 128px canvas; its 64px reference cell is bottom-left.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out'])
local action=app.params['action'] or 'VerticalSlash'
assert(action=='VerticalSlash' or action=='HorizontalSlash','Unsupported submitted action')
local number=action=='VerticalSlash' and 1 or 2
local count=number==1 and 9 or 8
local contact=number==1 and 5 or 4
assert(s.width==128 and s.height==128 and #s.frames==count,'Unexpected submitted attack revision')
local effect=nil;for _,layer in ipairs(s.layers) do if layer.name=='Effect' then effect=layer end end
local cel=assert(effect and effect:cel(contact),'Missing contact trail')
local minX,minY,maxX,maxY=9999,9999,-1,-1
for p in cel.image:pixels() do if app.pixelColor.rgbaA(p())>0 then
  local x,y=p.x+cel.position.x,p.y+cel.position.y
  minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x+1);maxY=math.max(maxY,y+1)
end end
assert(maxX>32 and maxY>minY,'Empty contact trail')
local boxMin=json.encode{x=math.max(0,(minX-32)/32),y=(s.height-maxY-38)/32}
local boxMax=json.encode{x=(maxX-32)/32,y=(s.height-minY-38)/32}
local guide=s.layers[1]
assert(guide.name=='Layer 1','Inspect the submission before changing guide visibility')
for _,cel in ipairs(guide.cels) do for p in cel.image:pixels() do
  local c=p();assert(app.pixelColor.rgbaA(c)==0 or c==app.pixelColor.rgba(0,0,0,255),'Guide contains artwork; inspect before export')
end end
guide.isVisible=false
-- Restore the existing trail without bringing back the older hidden sword drawing.
-- The current sword is already in Body. Keep Current/History byte-identical.
local restoreAttackLayers=action=='HorizontalSlash' and app.params['restore-attack-layers']~='false'
if restoreAttackLayers then
  for _,name in ipairs{'Effect'} do
    local found=nil;for _,layer in ipairs(s.layers) do if layer.name==name then found=layer end end
    assert(found,'Missing restored attack layer: '..name);found.isVisible=true
  end
end
local frames={};local eyes={};local native={};local cumulative=0;local start=0;local hitFrame=0;local branchFrame=0;local nativeMs=0
local sheet=Image(128*#s.frames,128,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  if i==contact then hitFrame=start end
  if i==count then branchFrame=start end
  nativeMs=nativeMs+math.floor(f.duration*1000+.5)
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
-- Keep native cel/recovery timing; only a reserved first-to-second branch is earlier.
local recoveryFrame=branchFrame
if action=='VerticalSlash' then branchFrame=29 end
sheet:saveAs(out..'/'..action..'.png')
local t=s:newTag(1,count);t.name=action
-- The editable review copy records the export visibility as well as its Action tag.
local review=out..'/Player_attack_'..number..'_preview.aseprite'
s:saveAs(review);s:close()
local reopen=assert(app.open(review))
assert(#reopen.frames==count and reopen.tags[1].name==action and not reopen.layers[1].isVisible)
if restoreAttackLayers then for _,layer in ipairs(reopen.layers) do if layer.name=='Effect' then assert(layer.isVisible) end end end
for _,f in ipairs(reopen.frames) do assert(math.floor(f.duration*1000+.5)==70) end
reopen:close()
local f=assert(io.open(out..'/timing-manifest.json','w'))
f:write(string.format('{"actions":[{"name":%q,"provisional":true,"includesSlashTrail":true,"hasTrailHitbox":true,"hitboxMin":%s,"hitboxMax":%s,"canvasWidth":128,"canvasHeight":128,"pivotX":32,"pivotY":38,"count":%d,"totalFrames":%d,"hitFrame":%d,"branchFrame":%d,"recoveryFrame":%d,"frames":[%s]}]}',action,boxMin,boxMax,count,start,hitFrame,branchFrame,recoveryFrame,table.concat(frames,',')));f:close()
local e=assert(io.open(out..'/eye-mask-manifest.json','w'));e:write('{"frames":['..table.concat(eyes,',')..']}');e:close()
local r=assert(io.open(out..'/submission-validation.json','w'));r:write(string.format('{"sourceUnmodified":true,"reviewCopyReopened":true,"guideHidden":true,"hairVisibilityPreserved":true,"restoredAttackLayers":%s,"nativeDurationMs":%d,"gameTicks":%d,"contactFrame":%d,"contactTick":%d,"frames":[%s]}',tostring(restoreAttackLayers),nativeMs,start,contact,hitFrame,table.concat(native,',')));r:close()

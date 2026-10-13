-- Run only on the human-authored cleanup document. Does not modify that document.
local input=assert(app.params['input']);local out=assert(app.params['out'])
local s=assert(app.open(input));assert(s.width==64 and s.height==64,'Expected 64x64 cleanup canvas')
local eyeLayer=nil
for _,l in ipairs(s.layers) do
  local name=string.lower(l.name)
  if name:find('rough') or name:find('guide') or name:find('reference') then l.isVisible=false end
  if name:find('eye glow mask') then l.isVisible=false;if name:find('cleanup') then eyeLayer=l end end
end
local specs={{'VerticalSlash',10,22,0,true},{'HorizontalSlash',9,20,0,true},{'RapidSheathe',11,32,0,false},{'IaiSlash',7,22,0,false},{'ParryDeflect',6,0,0,true},{'AirRoundhouse',28,36,18,true},
 {'Recovery_VerticalSlash',0,0,0,true},{'Recovery_HorizontalSlash',0,0,0,true},{'Recovery_RapidSheathe',0,0,0,false},{'Recovery_IaiSlash',0,0,0,false},{'Recovery_ParryDeflect',0,0,0,true},{'Recovery_AirRoundhouse',0,0,0,true},{'Recovery_Move',0,0,0,false},{'Recovery_Landing',0,0,0,false}}
-- Incremental handoff: list every currently completed tag. Unlisted actions stay rough in Unity.
if app.params['only'] then
  local requested={};for name in app.params['only']:gmatch('[^,]+') do requested[name:match('^%s*(.-)%s*$')]=true end
  local filtered={}
  for _,a in ipairs(specs) do if requested[a[1]] then
    requested[a[1]]=nil;a[5]=true;filtered[#filtered+1]=a
  end end
  for name in pairs(requested) do error('Unknown cleanup Action tag: '..name) end
  assert(#filtered>0,'No cleanup Action tags requested');specs=filtered
end
local manifest={};local eyeManifest={}
local function eyesFor(im,frame)
  local mask=Image(64,64,ColorMode.RGB);local count=0
  local cel=eyeLayer and eyeLayer:cel(frame) or nil
  if cel then
    for p in cel.image:pixels() do if app.pixelColor.rgbaA(p())>0 then count=count+1 end end
    if count>0 then mask:drawImage(cel.image,cel.position) end
  end
  if count==0 then
    for y=0,63 do for x=0,63 do
      local c=im:getPixel(x,y);local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
      if app.pixelColor.rgbaA(c)>0 and ((r==41 and g==58 and b==157) or (r==62 and g==79 and b==183) or (r==91 and g==110 and b==225)) then
        mask:drawPixel(x,y,app.pixelColor.rgba(255,255,255,255));count=count+1
      end
    end end
  end
  return mask,count
end
for _,a in ipairs(specs) do
  local tag=nil;for _,t in ipairs(s.tags) do if t.name==a[1] then tag=t;break end end
  assert(tag or not a[5],'Missing required Action tag: '..a[1])
  if tag then
    local images={};local durations={};local populated=true
    for frame=tag.fromFrame.frameNumber,tag.toFrame.frameNumber do
      local im=Image(s.spec);im:drawSprite(s,frame)
      local pixels=0;for p in im:pixels() do if app.pixelColor.rgbaA(p())>0 then pixels=pixels+1 end end
      if pixels==0 then populated=false end
      images[#images+1]=im;durations[#durations+1]=math.max(1,math.floor(s.frames[frame].duration*60+.5))
    end
    assert(populated or not a[5],'Required cleanup frame is empty: '..a[1])
    if populated then
      local elapsed=0;local frames={};local sheet=Image(#images*64,64,ColorMode.RGB);local startFrames={};local visibleEyes=0
      for i,im in ipairs(images) do
        im:saveAs(out..'/'..a[1]..'_'..string.format('%02d',i)..'.png');sheet:drawImage(im,Point((i-1)*64,0))
        local name=a[1]..'_'..string.format('%02d',i)
        local mask,count=eyesFor(im,tag.fromFrame.frameNumber+i-1);visibleEyes=visibleEyes+count
        mask:saveAs(out..'/EyeMasks/'..name..'.png');eyeManifest[#eyeManifest+1]=string.format('{"file":%q,"pixels":%d}',name,count)
        startFrames[elapsed]=true
        frames[#frames+1]=string.format('{"frames":%d,"start":%d}',durations[i],elapsed);elapsed=elapsed+durations[i]
      end
      assert(elapsed>a[2],'Cleanup ends before its hit: '..a[1]);sheet:saveAs(out..'/'..a[1]..'.png')
      local release,landing=0,0
      if a[1]=='AirRoundhouse' then
        assert(elapsed==36,'Roundhouse action must end at landing, 36F; author the standing return in Recovery_AirRoundhouse')
        for _,f in ipairs({18,24,28}) do assert(startFrames[f],'Missing roundhouse pose boundary at '..f..'F') end
        assert(visibleEyes>0,'Supply CleanUp - eye glow mask when the pupil colours change')
        release=24;landing=36
      end
      manifest[#manifest+1]=string.format('{"name":%q,"count":%d,"totalFrames":%d,"hitFrame":%d,"branchFrame":%d,"chargeFrame":%d,"releaseFrame":%d,"landingFrame":%d,"recoveryFrame":%d,"frames":[%s]}',a[1],#images,elapsed,a[2],a[3],a[4],release,landing,a[3],table.concat(frames,','))
    end
  end
end
s:close()
local f=assert(io.open(out..'/timing-manifest.json','w'));f:write('{"canvas":64,"ppu":32,"pivot":[32,38],"actions":['..table.concat(manifest,',')..']}');f:close()
local e=assert(io.open(out..'/eye-mask-manifest.json','w'));e:write('{"frames":['..table.concat(eyeManifest,',')..']}');e:close()

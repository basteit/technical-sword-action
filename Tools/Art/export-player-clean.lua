-- Run only on the human-authored cleanup document. Does not modify that document.
local input=assert(app.params['input']);local out=assert(app.params['out'])
local s=assert(app.open(input));assert(s.width==64 and s.height==64,'Expected 64x64 cleanup canvas')
for _,l in ipairs(s.layers) do
  local name=string.lower(l.name)
  if name:find('rough') or name:find('guide') or name:find('reference') then l.isVisible=false end
end
local specs={{'VerticalSlash',10,22,0,true},{'HorizontalSlash',9,20,0,true},{'RapidSheathe',11,32,0,false},{'IaiSlash',7,22,0,false},{'ParryDeflect',6,0,0,true},{'AirRoundhouse',28,54,18,true}}
local manifest={}
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
      local elapsed=0;local frames={};local sheet=Image(#images*64,64,ColorMode.RGB)
      for i,im in ipairs(images) do
        im:saveAs(out..'/'..a[1]..'_'..string.format('%02d',i)..'.png');sheet:drawImage(im,Point((i-1)*64,0))
        frames[#frames+1]=string.format('{"frames":%d,"start":%d}',durations[i],elapsed);elapsed=elapsed+durations[i]
      end
      assert(elapsed>a[2],'Cleanup ends before its hit: '..a[1]);sheet:saveAs(out..'/'..a[1]..'.png')
      manifest[#manifest+1]=string.format('{"name":%q,"count":%d,"totalFrames":%d,"hitFrame":%d,"branchFrame":%d,"chargeFrame":%d,"frames":[%s]}',a[1],#images,elapsed,a[2],a[3],a[4],table.concat(frames,','))
    end
  end
end
s:close()
local f=assert(io.open(out..'/timing-manifest.json','w'));f:write('{"canvas":64,"ppu":32,"pivot":[32,38],"actions":['..table.concat(manifest,',')..']}');f:close()

-- Keep received files unchanged. Export visible artwork and native frame timings.
local input=assert(app.params['input']);local out=assert(app.params['out'])
local names={'idle','walk','attack','chance','down'};local report={motions={}}
for _,name in ipairs(names) do
  local s=assert(app.open(input..'/EnemySample_'..name..'.aseprite'))
  local layers={};for _,l in ipairs(s.layers) do
    local guide=l.name=='Layer 1'
    if guide then for _,cel in ipairs(l.cels) do for p in cel.image:pixels() do
      local c=p();if app.pixelColor.rgbaA(c)>0 and (app.pixelColor.rgbaR(c)~=0 or app.pixelColor.rgbaG(c)~=0 or app.pixelColor.rgbaB(c)~=0) then guide=false end
    end end end
    if guide then l.isVisible=false end
    layers[#layers+1]={name=l.name,visible=l.isVisible,guideHidden=guide}
  end
  local frames={};local sheet=Image(s.width*#s.frames,s.height,ColorMode.RGB)
  for i,f in ipairs(s.frames) do
    local im=Image(s.spec);im:drawSprite(s,i)
    im:saveAs(out..'/EnemySample_'..name..'_'..string.format('%02d',i)..'.png')
    sheet:drawImage(im,Point((i-1)*s.width,0));frames[#frames+1]={durationMs=math.floor(f.duration*1000+.5)}
  end
  sheet:saveAs(out..'/EnemySample_'..name..'_sheet.png')
  report.motions[#report.motions+1]={name=name,width=s.width,height=s.height,layers=layers,frames=frames}
  s:close()
end
local f=assert(io.open(out..'/manifest.json','w'));f:write(json.encode(report));f:close()

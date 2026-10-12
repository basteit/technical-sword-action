-- Author in Aseprite, keeping the user's source documents untouched.
local root=assert(app.params['root']); local out=assert(app.params['out'])
local idle=app.open(root .. '/Assets/Art/Player/Player_idle.aseprite')
local walk=app.open(root .. '/Assets/Art/Player/Player_walk.aseprite')
assert(idle.width==64 and idle.height==64 and walk.width==64 and walk.height==64)
local base=Image(idle.spec);base:drawSprite(idle,1)
local C={outline=4281606178,dark=4282921268,hair=4293516504,white=4294702579,shade=4290816932,steel=4287003753,skin=4292208891}
local cyan=app.pixelColor.rgba(109,192,201,255)
local guideColor=app.pixelColor.rgba(228,155,83,255)
local function px(im,x,y,c) x=math.floor(x+.5);y=math.floor(y+.5);if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,c) end end
local function disk(im,x,y,r,c)
  for yy=math.floor(y-r),math.ceil(y+r) do for xx=math.floor(x-r),math.ceil(x+r) do
    if (xx-x)^2+(yy-y)^2<=r*r then px(im,xx,yy,c) end
  end end
end
local function line(im,a,b,w,c)
  local d=math.max(math.abs(b[1]-a[1]),math.abs(b[2]-a[2]),1)
  for i=0,d do local t=i/d;disk(im,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t,w/2,c) end
end
local function poly(im,p,c)
  for y=0,63 do
    local xs={}
    for i=1,#p do local a=p[i];local b=p[i%#p+1]
      if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
    end
    table.sort(xs);for i=1,#xs-1,2 do for x=math.ceil(xs[i]),math.floor(xs[i+1]) do px(im,x,y,c) end end
  end
end
local function limb(im,a,b,d,w,color)
  line(im,a,b,w+2,C.outline);line(im,b,d,w+2,C.outline)
  line(im,a,b,w,color);line(im,b,d,w,color)
end
local function head(im,hx,hy,angle,back)
  local co=math.cos(angle*math.pi/180);local si=math.sin(angle*math.pi/180)
  -- The entire upper silhouette is sampled from the supplied first idle cel.
  for y=math.floor(hy-25),math.ceil(hy+22) do for x=math.floor(hx-26),math.ceil(hx+22) do
    local dx=x-hx;local dy=y-hy
    local sx=math.floor(33+dx*co+dy*si+.5);local sy=math.floor(14-dx*si+dy*co+.5)
    if sx>=9 and sx<=47 and sy>=0 and sy<=24 then
      local c=base:getPixel(sx,sy)
      if back and sx>=27 and sx<=43 and sy>=9 and sy<=23 and app.pixelColor.rgbaA(c)>0 then
        c=(sx+sy)%5==0 and C.shade or C.hair
      end
      if app.pixelColor.rgbaA(c)>0 then px(im,x,y,c) end
    end
  end end
end
local function pose(im,weapon,p)
  local x=p.x or 32;local hy=p.hy or 14;local hip=p.hip or {x,40};local neck={p.hx or x,hy+10}
  local left=p.left or {{x-7,47},{x-9,56}};local right=p.right or {{x+4,48},{x+6,56}}
  -- Trailing ponytail, then legs, dress and arms, then the unchanged reference head.
  poly(im,{{neck[1]-6,neck[2]-5},{neck[1]-16,neck[2]+8},{neck[1]-21,neck[2]+23},{neck[1]-11,neck[2]+14},{neck[1]-3,neck[2]+1}},C.shade)
  line(im,{neck[1]-9,neck[2]+1},{neck[1]-18,neck[2]+17},3,C.hair)
  limb(im,{hip[1]-3,hip[2]},left[1],left[2],3,C.dark)
  limb(im,{hip[1]+3,hip[2]},right[1],right[2],3,C.outline)
  line(im,{left[2][1]-2,left[2][2]},{left[2][1]+3,left[2][2]},3,C.outline)
  line(im,{right[2][1]-2,right[2][2]},{right[2][1]+4,right[2][2]},3,C.outline)
  px(im,math.min(63,right[2][1]+3),right[2][2]-1,C.hair)
  poly(im,{{neck[1]-5,neck[2]},{neck[1]+5,neck[2]},{hip[1]+5,hip[2]-5},{hip[1]+9,hip[2]+4},{hip[1]-9,hip[2]+4},{hip[1]-5,hip[2]-5}},C.outline)
  poly(im,{{neck[1]-3,neck[2]+1},{neck[1]+3,neck[2]+1},{hip[1]+3,hip[2]-7},{hip[1]-4,hip[2]-7}},p.back and C.dark or C.white)
  poly(im,{{hip[1]-5,hip[2]-5},{hip[1]+4,hip[2]-5},{hip[1]+8,hip[2]+3},{hip[1]-8,hip[2]+3}},C.dark)
  line(im,{hip[1]-5,hip[2]-6},{hip[1]+5,hip[2]-6},2,C.steel)
  for i=-5,5,4 do line(im,{hip[1]+i,hip[2]-3},{hip[1]+i+1,hip[2]+2},1,C.shade) end
  local hand=p.hand or {x+11,34};local elbow=p.elbow or {x+7,29}
  limb(im,{neck[1]+4,neck[2]+1},elbow,hand,3,C.white)
  disk(im,hand[1],hand[2],1.5,C.skin)
  local lh=p.lhand or {x-7,36};limb(im,{neck[1]-4,neck[2]+1},{x-8,30},lh,3,C.shade)
  head(im,p.hx or x,hy,p.angle or 0,p.back)
  -- Sheath belongs to the body; blade motion is independently editable.
  line(im,{hip[1]-5,hip[2]-7},{hip[1]-15,hip[2]+11},2,C.outline)
  if p.tip then
    local dx=p.tip[1]-hand[1];local dy=p.tip[2]-hand[2];local len=math.max(math.sqrt(dx*dx+dy*dy),1)
    local hilt={hand[1]-dx/len*5,hand[2]-dy/len*5}
    line(weapon,hilt,hand,2,C.dark)
    line(weapon,{hand[1]-dy/len*3,hand[2]+dx/len*3},{hand[1]+dy/len*3,hand[2]-dx/len*3},2,C.steel)
    line(weapon,hand,p.tip,3,C.steel);line(weapon,hand,p.tip,1,C.white)
  end
  if p.smear then
    for i=1,#p.smear-1 do line(weapon,p.smear[i],p.smear[i+1],1,cyan) end
  end
end
local actions={
 {name='VerticalSlash',frames={4,3,3,2,2,4,4,6},hit=10,branch=22,poses={
  {hand={33,23},tip={26,4}}, {hy=16,hand={35,17},tip={27,0}}, {hand={38,13},tip={37,0}},
  {x=34,hx=35,hy=16,hand={44,29},tip={52,60},smear={{39,0},{58,16},{60,40}}},
  {x=35,hy=16,hand={45,34},tip={49,59}}, {x=34,hand={43,38},tip={49,59}},
  {hand={40,36},tip={53,45}}, {hand={40,34},tip={53,41}}
 }},
 {name='HorizontalSlash',frames={3,3,3,2,2,3,4,6},hit=9,branch=20,poses={
  {hand={26,31},tip={8,25}}, {hx=30,hy=16,hand={24,32},tip={3,30}}, {x=33,hy=16,hand={36,32},tip={54,28}},
  {x=35,hx=35,hy=15,hand={43,32},tip={63,32},smear={{5,31},{25,27},{48,29},{63,32}}},
  {x=34,hand={44,33},tip={62,36}}, {hand={43,36},tip={61,41}},
  {hand={39,36},tip={55,47}}, {hand={40,34},tip={53,41}}
 }},
 {name='RapidSheathe',frames={3,3,3,2,2,2,5,6,12},hit=11,branch=32,poses={
  {hand={31,28},tip={16,7}}, {hand={39,25},tip={56,4}}, {hand={43,33},tip={61,47},smear={{34,1},{58,14},{63,42}}},
  {hand={26,32},tip={4,35},smear={{5,31},{29,27},{63,31}}},
  {hand={43,30},tip={62,13},smear={{12,38},{35,22},{61,10}}}, {hand={42,35},tip={61,51}},
  {hand={35,35},tip={53,46}}, {hand={29,35},tip={16,48}}, {hand={27,35},lhand={25,37}}
 }},
 {name='IaiSlash',frames={4,3,2,2,3,4,6,6},hit=7,branch=22,poses={
  {hy=16,hand={27,35},lhand={25,37}}, {hy=17,hand={31,34},tip={43,34}},
  {x=36,hx=35,hy=16,hand={45,30},tip={63,23},smear={{8,39},{26,34},{63,23}}},
  {x=35,hand={46,29},tip={63,19}}, {hand={42,31},tip={62,24}},
  {hand={39,35},tip={57,44}}, {hand={30,36},tip={20,46}}, {hand={27,35},lhand={25,37}}
 }},
 {name='ParryDeflect',frames={3,3,2,2,4,5},hit=6,branch=0,poses={
  {hy=16,hand={38,30},tip={50,15}}, {hand={43,27},tip={51,8}},
  {x=33,hand={45,26},tip={60,9},smear={{43,42},{56,25},{60,8}}},
  {hand={44,25},tip={58,10}}, {hand={43,31},tip={57,21}}, {hand={40,34},tip={53,41}}
 }},
 {name='AirRoundhouse',frames={4,4,4,4,2,3,3,2,2,2,3,3,4,4},hit=28,charge=18,release=24,landing=36,branch=44,poses={
  {hy=17,hip={32,42},hand={28,34},left={{25,49},{23,57}},right={{39,48},{40,57}}},
  {hy=16,hip={32,39},hand={28,32},left={{26,47},{23,55}},right={{39,44},{35,48}}},
  {hx=30,hy=13,hip={33,37},hand={29,29},left={{39,42},{42,49}},right={{26,39},{18,42}}},
  {hx=32,hy=14,hip={32,35},angle=-8,back=true,hand={40,31},left={{39,42},{43,47}},right={{25,35},{18,34}}},
  {hx=30,hy=15,hip={32,34},angle=-12,back=true,hand={39,31},left={{38,43},{40,47}},right={{28,30},{25,27}}},
  {hx=28,hy=16,hip={33,34},angle=-16,back=true,hand={38,31},left={{25,42},{21,45}},right={{41,29},{34,24}}},
  {hx=26,hy=16,hip={34,34},angle=-20,hand={27,30},left={{24,42},{20,45}},right={{45,29},{42,25}}},
  {hx=25,hy=17,hip={34,34},angle=-22,hand={26,31},left={{24,42},{19,45}},right={{46,31},{50,29}},smear={{44,27},{49,28},{54,30}}},
  {hx=25,hy=17,hip={34,35},angle=-20,hand={25,30},left={{24,42},{20,46}},right={{46,34},{57,33}},smear={{48,28},{54,30},{60,34}}},
  {hx=25,hy=17,hip={34,35},angle=-18,hand={24,30},left={{24,42},{20,46}},right={{47,35},{61,34}},smear={{32,10},{53,19},{62,34}}},
  {hx=27,hy=15,hip={33,36},angle=-10,hand={26,29},left={{26,42},{25,48}},right={{44,38},{55,44}}},
  {hx=28,hy=23,hip={33,41},angle=-16,hand={40,45},lhand={24,43},left={{29,46},{26,53}},right={{41,45},{44,50}}},
  {hx=28,hy=25,hip={32,43},angle=-8,hand={45,55},lhand={25,47},left={{28,50},{25,57}},right={{39,51},{43,57}}},
  {idle=true}
 }}
}
local s=Sprite(64,64,ColorMode.RGB)
local reference=s.layers[1];reference.name='Reference - original idle and walk (toggle)';reference.isVisible=false;reference.isEditable=false
local guides=s:newLayer();guides.name='Guides - ground COM trajectory (toggle)';guides.isVisible=false;guides.opacity=96;guides.isEditable=false
local rough=s:newLayer();rough.name='Rough - pose'
local blade=s:newLayer();blade.name='Rough - weapon and motion arc'
local clean=s:newLayer();clean.name='CleanUp - body';local cleanBlade=s:newLayer();cleanBlade.name='CleanUp - weapon'
local eyeGuide=s:newLayer();eyeGuide.name='Guides - eye glow mask (toggle)';eyeGuide.isVisible=false
local cleanEyes=s:newLayer();cleanEyes.name='CleanUp - eye glow mask';cleanEyes.isVisible=false
local index=0; local manifest={};local eyeManifest={};local tagBounds={};local csv={'Action,LocalFrame,DocumentFrame,GameFrames,DurationMs,StartGameFrame,Phase,Event'}
local function exportEyes(im,name)
  -- These three reference colours occur only in the pupils, not the hair or accessory.
  local mask=Image(64,64,ColorMode.RGB);local count=0
  for y=0,63 do for x=0,63 do
    local c=im:getPixel(x,y);local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
    if app.pixelColor.rgbaA(c)>0 and ((r==41 and g==58 and b==157) or (r==62 and g==79 and b==183) or (r==91 and g==110 and b==225)) then
      mask:drawPixel(x,y,app.pixelColor.rgba(255,255,255,255));count=count+1
    end
  end end
  mask:saveAs(out..'/EyeMasks/'..name..'.png')
  eyeManifest[#eyeManifest+1]=string.format('{"file":%q,"pixels":%d}',name,count)
  s:newCel(eyeGuide,index,mask,Point(0,0));s:newCel(cleanEyes,index,Image(64,64,ColorMode.RGB),Point(0,0))
end
local function frame(duration)
  index=index+1;if index>1 then s:newEmptyFrame(index) end
  s.frames[index].duration=duration
end
for _,r in ipairs({{sprite=idle,name='IdleReference'},{sprite=walk,name='WalkReference'}}) do
  local first=index+1;local sheet=Image(#r.sprite.frames*64,64,ColorMode.RGB)
  for i,f in ipairs(r.sprite.frames) do
    frame(f.duration);local im=Image(r.sprite.spec);im:drawSprite(r.sprite,i)
    s:newCel(reference,index,im,Point(0,0));sheet:drawImage(im,Point((i-1)*64,0))
    local name=r.name .. '_' .. string.format('%02d',i)
    im:saveAs(out .. '/' .. name .. '.png');exportEyes(im,name)
  end
  local tag=s:newTag(first,index);tag.name=r.name
  tagBounds[#tagBounds+1]={name=r.name,first=first,last=index,tag=tag}
  sheet:saveAs(out .. '/' .. r.name .. '.png')
end
for _,a in ipairs(actions) do
  local first=index+1;local sheet=Image(#a.frames*64,64,ColorMode.RGB);local start=0;local mf={}
  for i,n in ipairs(a.frames) do
    frame(math.floor(n/60*1000+.5)/1000)
    s:newCel(reference,index,Image(base),Point(0,0))
    local im=Image(64,64,ColorMode.RGB);local wi=Image(64,64,ColorMode.RGB)
    if a.poses[i].idle then im=Image(base) else pose(im,wi,a.poses[i]) end
    s:newCel(rough,index,im,Point(0,0));s:newCel(blade,index,wi,Point(0,0))
    s:newCel(clean,index,Image(64,64,ColorMode.RGB),Point(0,0));s:newCel(cleanBlade,index,Image(64,64,ColorMode.RGB),Point(0,0))
    local gi=Image(64,64,ColorMode.RGB)
    for x=0,63,3 do px(gi,x,58,guideColor) end
    for y=0,63,3 do px(gi,32,y,cyan) end
    disk(gi,a.poses[i].hip and a.poses[i].hip[1] or 32,a.poses[i].hip and a.poses[i].hip[2] or 40,2,guideColor)
    local gc=s:newCel(guides,index,gi,Point(0,0));gc.data=a.name .. ' / ' .. i .. ' / ' .. n .. ' game frames; floor row 58; root pivot (32,38 bottom-origin)'
    im:drawImage(wi);sheet:drawImage(im,Point((i-1)*64,0))
    local name=a.name .. '_' .. string.format('%02d',i)
    im:saveAs(out .. '/' .. name .. '.png');exportEyes(im,name)
    local event='';if start==a.hit then event='Hit' end;if a.charge and start==a.charge then event='ChargeSlow' end
    if start==a.release then event='ReleaseSlow' end;if start==a.landing then event='Landing' end
    local phase=start<a.hit and 'anticipation' or start<a.hit+4 and 'active' or start<(a.branch or 999) and 'cancel-lock' or 'recovery'
    if a.release and start>=a.release and start<a.hit then phase='fast-swing' end
    if a.landing and start>=a.landing then phase=start<40 and 'ukemi' or 'stand' end
    csv[#csv+1]=string.format('%s,%d,%d,%d,%d,%d,%s,%s',a.name,i,index,n,math.floor(n/60*1000+.5),start,phase,event)
    mf[#mf+1]=string.format('{"frames":%d,"start":%d}',n,start);start=start+n
  end
  local t=s:newTag(first,index);t.name=a.name
  tagBounds[#tagBounds+1]={name=a.name,first=first,last=index,tag=t}
  sheet:saveAs(out .. '/' .. a.name .. '.png')
  manifest[#manifest+1]=string.format('{"name":%q,"count":%d,"totalFrames":%d,"hitFrame":%d,"branchFrame":%d,"chargeFrame":%d,"releaseFrame":%d,"landingFrame":%d,"frames":[%s]}',a.name,#a.frames,start,a.hit,a.branch or 0,a.charge or 0,a.release or 0,a.landing or 0,table.concat(mf,','))
end
-- Aseprite extends a tag when a new frame is appended at its end. Restore all
-- intended ranges only after creating the entire document.
for _,b in ipairs(tagBounds) do b.tag.fromFrame=b.first;b.tag.toFrame=b.last end
local file=out .. '/Player_actions_rough.aseprite';s:saveAs(file);s:close()
local reopen=app.open(file)
assert(#reopen.layers==8 and #reopen.tags==8 and #reopen.frames==index)
for _,b in ipairs(tagBounds) do
  local found=false
  for _,tag in ipairs(reopen.tags) do if tag.name==b.name then
    assert(tag.fromFrame.frameNumber==b.first and tag.toFrame.frameNumber==b.last,'Incorrect reopened tag range: '..b.name);found=true
  end end
  assert(found)
end
reopen:close();idle:close();walk:close()
local f=assert(io.open(out .. '/timing-manifest.json','w'));f:write('{"canvas":64,"ppu":32,"pivot":[32,38],"actions":[' .. table.concat(manifest,',') .. ']}');f:close()
local c=assert(io.open(out .. '/timing.csv','w'));c:write(table.concat(csv,'\n'));c:close()
local e=assert(io.open(out..'/eye-mask-manifest.json','w'));e:write('{"frames":['..table.concat(eyeManifest,',')..']}');e:close()
local report=assert(io.open(out .. '/authoring-validation.json','w'));report:write(string.format('{"reopened":true,"tagRangesChecked":true,"layers":8,"tags":8,"frames":%d,"sourceFilesModified":false}',index));report:close()

local out=assert(app.params['out'])
local rgba=app.pixelColor.rgba
local P={ink=rgba(21,28,42,255),dark=rgba(35,48,62,255),metal=rgba(66,85,100,255),edge=rgba(112,140,150,255),gold=rgba(212,165,80,255),light=rgba(255,226,158,255),white=rgba(255,248,222,255),teal=rgba(100,200,207,255)}
local function px(im,x,y,c) x=math.floor(x+.5);y=math.floor(y+.5);if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,c) end end
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do px(im,xx,yy,c) end end end
local function line(im,x,y,xx,yy,w,c)
  local n=math.max(math.abs(xx-x),math.abs(yy-y),1)
  for i=0,n do local t=i/n;rect(im,math.floor(x+(xx-x)*t+.5),math.floor(y+(yy-y)*t+.5),w,w,c) end
end
local function poly(im,p,c)
  for y=0,im.height-1 do local a={};for i=1,#p do local u=p[i];local v=p[i%#p+1]
    if (u[2]<=y and v[2]>y) or (v[2]<=y and u[2]>y) then a[#a+1]=u[1]+(y-u[2])*(v[1]-u[1])/(v[2]-u[2]) end end
    table.sort(a);for i=1,#a-1,2 do for x=math.ceil(a[i]),math.floor(a[i+1]) do px(im,x,y,c) end end
  end
end
local function ring(im,cx,cy,r,width,c,squash)
  squash=squash or 1
  for y=math.floor(cy-r*squash-width),math.ceil(cy+r*squash+width) do for x=math.floor(cx-r-width),math.ceil(cx+r+width) do
    local d=math.sqrt((x-cx)^2+((y-cy)/squash)^2)
    if math.abs(d-r)<=width/2 then px(im,x,y,c) end
  end end
end
local E={ink=rgba(48,60,78,255),body=rgba(205,213,224,255),joint=rgba(133,148,167,255),amber=rgba(233,123,40,255),red=rgba(212,61,58,255),blue=rgba(39,145,184,255)}
local function disk(im,cx,cy,r,c)
  for y=cy-r,cy+r do for x=cx-r,cx+r do if (x-cx)^2+(y-cy)^2<=r*r then px(im,x,y,c) end end end
end
local function enemy(im,n,state)
  -- A training dummy: one round head, one chest indicator, simple joints and rod.
  local hx,hy=32,16;local hipx,hipy=32,39;local core=E.joint
  local hand={45,33};local tip={57,45};local left={25,55};local right={39,55}
  if state=='Idle' then hy=16+((n==2 or n==3) and 1 or 0) end
  if state=='Move' then hx=34;left={24+(n%3)*2,55};right={40-(n%3)*2,55} end
  if state=='Windup' then hx=30-math.floor(n/3);hy=17;hand={39,23-math.floor(n/2)};tip={43-n,3};core=n<5 and E.amber or E.red end
  if state=='Attack' then hx=36;hy=19;hipx=34;hand={46,28+n};tip={60,19+n*6};core=E.red end
  if state=='Hit' then hx=27;hy=20;hipx=30;hand={39,37};tip={50,50} end
  if state=='Parried' then local recoil=({3,10,8,6})[n];hx=32-recoil;hy=20;hipx=30;hand={40-recoil,24};tip={47-recoil,7};core=E.blue end
  if state=='Death' then
    local spread=(n-1)*2
    disk(im,30-spread,29+math.min(n,3),6,E.ink);disk(im,30-spread,29+math.min(n,3),4,E.body)
    rect(im,29+spread,38,12,12,E.ink);rect(im,31+spread,40,8,8,E.body)
    line(im,27-spread,44,18-spread,53,3,E.ink);line(im,37+spread,45,48+spread,53,3,E.ink)
    line(im,45,39,58,45,2,E.ink)
    return
  end
  line(im,hipx-3,hipy,left[1],left[2],4,E.ink);line(im,hipx+3,hipy,right[1],right[2],4,E.ink)
  line(im,left[1]-3,left[2],left[1]+3,left[2],3,E.ink);line(im,right[1]-3,right[2],right[1]+3,right[2],3,E.ink)
  disk(im,hipx,hipy,3,E.joint)
  line(im,hx,hy+6,hipx,hipy,12,E.ink);line(im,hx,hy+7,hipx,hipy-3,8,E.body)
  local cx=math.floor((hx+hipx)/2+.5);rect(im,cx-3,29,6,6,core)
  line(im,hx+5,hy+12,hand[1],hand[2],4,E.ink);disk(im,hand[1],hand[2],2,E.joint)
  line(im,hx-5,hy+12,hipx-10,36,4,E.ink);disk(im,hipx-10,36,2,E.joint)
  disk(im,hx,hy,7,E.ink);disk(im,hx,hy,5,E.body)
  line(im,hx+2,hy-2,hx+2,hy+2,2,E.ink)
  line(im,hand[1]-2,hand[2]+2,tip[1],tip[2],3,E.ink)
  line(im,hand[1],hand[2],tip[1],tip[2],1,(state=='Windup' or state=='Attack') and E.red or E.joint)
  if state=='Windup' then
    ring(im,tip[1],tip[2]+4,3+(n>=5 and 1 or 0),1,core)
    if n>=5 then line(im,tip[1]-6,tip[2]+4,tip[1]-4,tip[2]+4,1,core);line(im,tip[1]+4,tip[2]+4,tip[1]+6,tip[2]+4,1,core) end
  end
end
local motions={{'Idle',4},{'Move',6},{'Windup',6},{'Attack',4},{'Hit',3},{'Parried',4},{'Death',6}}
local s=Sprite(64,64,ColorMode.RGB);s.layers[1].name='Training dummy - body joints timing rod';local index=0;local enemyBounds={}
for _,m in ipairs(motions) do local first=index+1;local sheet=Image(m[2]*64,64,ColorMode.RGB)
  for n=1,m[2] do index=index+1;if index>1 then s:newEmptyFrame(index) end;s.frames[index].duration=m[1]=='Idle' and .133 or m[1]=='Windup' and .083 or .067
    local im=Image(64,64,ColorMode.RGB);enemy(im,n,m[1]);s:newCel(s.layers[1],index,im,Point(0,0))
    im:saveAs(out .. '/Enemy_' .. m[1] .. '_' .. string.format('%02d',n) .. '.png');sheet:drawImage(im,Point((n-1)*64,0))
  end
  local tag=s:newTag(first,index);tag.name=m[1];sheet:saveAs(out .. '/Enemy_' .. m[1] .. '.png')
  enemyBounds[#enemyBounds+1]={name=m[1],first=first,last=index,tag=tag}
end
for _,b in ipairs(enemyBounds) do b.tag.fromFrame=b.first;b.tag.toFrame=b.last end
s:newLayer().name='CleanUp';s:saveAs(out .. '/CaptureDummy.aseprite');s:close()
local function validateTags(path,bounds,frameCount)
  local reopened=app.open(path);assert(#reopened.frames==frameCount and #reopened.tags==#bounds)
  for _,b in ipairs(bounds) do local found=false
    for _,t in ipairs(reopened.tags) do if t.name==b.name then
      assert(t.fromFrame.frameNumber==b.first and t.toFrame.frameNumber==b.last,'Incorrect tag range: '..b.name);found=true
    end end
    assert(found)
  end
  reopened:close()
end
validateTags(out..'/CaptureDummy.aseprite',enemyBounds,index)
if app.params['part']=='enemy' then return end
local b=Sprite(384,216,ColorMode.RGB);b.layers[1].name='White training grid';local im=Image(384,216,ColorMode.RGB)
rect(im,0,0,384,216,rgba(250,251,253,255))
rect(im,0,157,384,59,rgba(245,247,250,255))
for x=0,383,16 do line(im,x,0,x,215,1,rgba(226,232,239,255)) end
for y=12,215,16 do line(im,0,y,383,y,1,rgba(226,232,239,255)) end
for x=0,383,64 do line(im,x,0,x,215,1,rgba(206,216,228,255)) end
for y=28,215,64 do line(im,0,y,383,y,1,rgba(206,216,228,255)) end
line(im,0,156,383,156,2,rgba(128,148,172,255))
b:newCel(b.layers[1],1,im,Point(0,0));b:saveAs(out .. '/CaptureTrainingGrid.aseprite');im:saveAs(out .. '/CaptureTrainingGrid.png');b:close()
local fx=Sprite(128,128,ColorMode.RGB);fx.layers[1].name='Pixel effect';index=0;local fxBounds={}
local fxTimes={Slash={1,1,1,1,2,2,2,2},Parry={2,1,1,1,1,2,2,2},Kick={1,1,2,3,3,3,3,3},Vanish={3,3,3,3,3,3,3,3}}
for _,kind in ipairs({'Slash','Parry','Kick','Vanish'}) do local first=index+1;local sheet=Image(128*8,128,ColorMode.RGB)
  for f=1,8 do index=index+1;if index>1 then fx:newEmptyFrame(index) end;fx.frames[index].duration=math.floor(fxTimes[kind][f]/60*1000+.5)/1000
    local img=Image(128,128,ColorMode.RGB);local alpha=math.floor(255*(1-(f-1)/8));local gold=rgba(210,126,35,alpha);local white=rgba(255,246,213,alpha);local teal=rgba(38,148,177,alpha)
    if kind=='Slash' then
      for i=0,80 do local a=(-1.15+i/80*2.0);local r=16+f*2;local x=52+math.cos(a)*r;local y=64+math.sin(a)*r*.85
        if i<80-f*5 then rect(img,math.floor(x),math.floor(y),2,2,i>48 and white or teal) end
      end
      line(img,58,65,70+f*2,62-f,1,white)
    elseif kind=='Parry' then
      if f<=3 then
        ring(img,64,64,5+f*5,1,gold)
        line(img,32-f*2,89+f,94+f,37-f,3-f/2,white)
        line(img,39-f,87,91,44-f,1,gold)
      end
      for k=0,9 do local a=k*math.pi/5+.25;local r=9+f*4
        local length=math.max(2,13-f)+(k%3)*2
        line(img,64+math.cos(a)*r,64+math.sin(a)*r,64+math.cos(a)*(r+length),64+math.sin(a)*(r+length),k%3==0 and 2 or 1,k%2==0 and gold or white)
      end
      if f==1 then poly(img,{{64,50},{67,61},{76,64},{67,67},{64,78},{61,67},{52,64},{61,61}},P.white) end
    elseif kind=='Kick' then
      ring(img,64,64,8+f*6,math.max(1,6-f),gold,.84);ring(img,64,64,5+f*6,1,white,.84)
      if f==1 then poly(img,{{64,49},{67,60},{79,64},{67,68},{64,79},{61,68},{49,64},{61,60}},white) end
      for k=0,15 do local a=k*math.pi/8+.12;local r=9+f*4;local len=8+(k%4)*4
        line(img,64+math.cos(a)*r,64+math.sin(a)*r*.84,64+math.cos(a)*(r+len),64+math.sin(a)*(r+len)*.84,k%3==0 and 2 or 1,k%2==0 and gold or white)
      end
      for k=0,7 do local a=k*.8;rect(img,math.floor(64+math.cos(a)*f*5),math.floor(64+math.sin(a)*f*4),2,2,teal) end
    else
      for k=1,19 do local a=k*2.399;local r=3+f*(1+k%5);local x=64+math.cos(a)*r;local y=64+math.sin(a)*r-f*2
        poly(img,{{x,y-2},{x+2,y},{x,y+2},{x-2,y}},k%3==0 and teal or gold)
      end
    end
    -- Keep white highlights readable against the white training grid.
    local edged=Image(128,128,ColorMode.RGB)
    for y=0,127 do for x=0,127 do local c=img:getPixel(x,y)
      if app.pixelColor.rgbaA(c)>0 then
        for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do px(edged,x+d[1],y+d[2],rgba(64,73,91,math.floor(app.pixelColor.rgbaA(c)*.8))) end
      end
    end end
    edged:drawImage(img,Point(0,0));img=edged
    fx:newCel(fx.layers[1],index,img,Point(0,0));img:saveAs(out .. '/Fx_' .. kind .. '_' .. string.format('%02d',f) .. '.png');sheet:drawImage(img,Point((f-1)*128,0))
  end
  local tag=fx:newTag(first,index);tag.name=kind;sheet:saveAs(out .. '/Fx_' .. kind .. '.png')
  fxBounds[#fxBounds+1]={name=kind,first=first,last=index,tag=tag}
end
for _,b in ipairs(fxBounds) do b.tag.fromFrame=b.first;b.tag.toFrame=b.last end
fx:newLayer().name='Glow overlay (optional, runtime intensity)';fx:saveAs(out .. '/CombatEffects.aseprite');fx:close()
validateTags(out..'/CombatEffects.aseprite',fxBounds,index)
local report=assert(io.open(out..'/authoring-validation.json','w'));report:write('{"reopened":true,"tagRangesChecked":true,"enemyFrames":33,"enemyTags":7,"effectFrames":32,"effectTags":4}');report:close()

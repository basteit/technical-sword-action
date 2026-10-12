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
local function enemy(im,n,state)
  local x=31;local dy=0;local tilt=0
  if state=='Idle' then dy=(n==2 or n==3) and 1 or 0 end
  if state=='Move' then dy=n%2;tilt=(n%2)*2 end
  if state=='Windup' then x=30-n;dy=math.floor(n/3) end
  if state=='Attack' then x=33+n;dy=1 end
  if state=='Hit' then x=32-math.min(n,3);dy=2 end
  if state=='Parried' then x=29;dy=3;tilt=-3 end
  if state=='Death' and n<=2 then state='Hit';x=32-n;dy=2+n end
  if state=='Death' then
    local q=math.min(n/6,1)
    poly(im,{{24,38},{40,35},{48,48},{44,56},{17,56}},P.ink)
    rect(im,25+math.floor(q*8),41,16,8,P.metal);rect(im,30+math.floor(q*8),42,9,3,P.gold)
    line(im,18,52,52,55,3,P.edge);line(im,26,53,20,57,3,P.dark)
    for k=1,5 do rect(im,18+k*6,48+(k%3)*2,2,2,P.gold) end
    return
  end
  -- Masked bronze sentinel: cloak, segmented greaves, shoulder plates, blade.
  poly(im,{{x-11,23+dy},{x+7,24+dy},{x+13,52},{x-17,55}},P.ink)
  for k=0,3 do line(im,x-10+k*5,31+dy,x-15+k*6,52,1,P.dark) end
  line(im,x-5,40+dy,x-8-tilt,53,5,P.dark);line(im,x+5,40+dy,x+8+tilt,53,5,P.metal)
  rect(im,x-11-tilt,54,8,4,P.ink);rect(im,x+4+tilt,54,9,4,P.ink)
  rect(im,x-10-tilt,54,7,1,P.edge);rect(im,x+5+tilt,54,7,1,P.gold)
  poly(im,{{x-9,24+dy},{x+9,24+dy},{x+8,39+dy},{x-8,39+dy}},P.metal)
  poly(im,{{x-8,25+dy},{x+8,25+dy},{x+6,29+dy},{x-6,30+dy}},P.edge)
  line(im,x-6,31+dy,x+6,31+dy,2,P.gold);line(im,x-5,35+dy,x+5,35+dy,1,P.dark)
  rect(im,x-7,39+dy,15,4,P.ink);rect(im,x-3,40+dy,5,2,P.gold)
  poly(im,{{x-10,9+dy},{x+8,10+dy},{x+10,23+dy},{x+3,28+dy},{x-8,22+dy}},P.ink)
  poly(im,{{x-8,10+dy},{x+6,11+dy},{x+8,21+dy},{x+2,24+dy},{x-7,20+dy}},P.metal)
  poly(im,{{x+1,12+dy},{x+6,12+dy},{x+8,20+dy},{x+1,23+dy}},P.edge)
  line(im,x-6,18+dy,x+7,17+dy,2,P.ink);line(im,x-3,18+dy,x+6,17+dy,1,P.light)
  line(im,x-6,10+dy,x-12,3+dy,2,P.gold);line(im,x+3,10+dy,x+8,5+dy,2,P.edge)
  poly(im,{{x-13,24+dy},{x-7,23+dy},{x-6,30+dy},{x-14,31+dy}},P.edge)
  poly(im,{{x+6,24+dy},{x+13,24+dy},{x+14,32+dy},{x+7,31+dy}},P.gold)
  local hx,hy=x+15,36+dy;local tx,ty=54,45
  if state=='Windup' then hx=x+12;hy=24-n;tx=x+17-n;ty=2 end
  if state=='Attack' then hx=44;hy=31;tx=59;ty=22+(n-1)*5 end
  if state=='Parried' then hx=40;hy=30;tx=54;ty=math.max(7,21-n*4) end
  if state=='Hit' then hx=40;hy=38;tx=54;ty=47 end
  line(im,x+10,28+dy,hx,hy,4,P.dark);rect(im,hx,hy,3,3,P.gold)
  line(im,hx-3,hy+2,tx,ty,3,P.ink);line(im,hx,hy,tx,ty,2,P.edge);line(im,hx,hy,tx,ty,1,P.light)
  if state=='Windup' and n>=3 then ring(im,tx,ty+5,3,1,P.gold) end
end
local motions={{'Idle',4},{'Move',6},{'Windup',6},{'Attack',4},{'Hit',3},{'Parried',4},{'Death',6}}
local s=Sprite(64,64,ColorMode.RGB);s.layers[1].name='Sentinel - armor cloak blade';local index=0
for _,m in ipairs(motions) do local first=index+1;local sheet=Image(m[2]*64,64,ColorMode.RGB)
  for n=1,m[2] do index=index+1;if index>1 then s:newEmptyFrame(index) end;s.frames[index].duration=m[1]=='Idle' and .133 or m[1]=='Windup' and .083 or .067
    local im=Image(64,64,ColorMode.RGB);enemy(im,n,m[1]);s:newCel(s.layers[1],index,im,Point(0,0))
    im:saveAs(out .. '/Enemy_' .. m[1] .. '_' .. string.format('%02d',n) .. '.png');sheet:drawImage(im,Point((n-1)*64,0))
  end
  local tag=s:newTag(first,index);tag.name=m[1];sheet:saveAs(out .. '/Enemy_' .. m[1] .. '.png')
end
s:newLayer().name='CleanUp';s:saveAs(out .. '/CaptureSentinel.aseprite');s:close()
local b=Sprite(384,216,ColorMode.RGB);b.layers[1].name='Far architecture';local im=Image(384,216,ColorMode.RGB)
rect(im,0,0,384,216,rgba(18,28,43,255))
for y=0,155 do rect(im,0,y,384,1,rgba(15+math.floor(y/18),24+math.floor(y/13),39+math.floor(y/10),255)) end
for x=0,383,32 do line(im,x,25,x,150,1,rgba(34,51,66,255)) end
for y=30,150,24 do line(im,0,y,383,y,1,rgba(31,48,62,255)) end
ring(im,196,61,38,2,rgba(44,69,82,255));ring(im,196,61,31,1,rgba(50,76,88,255));ring(im,196,61,24,1,rgba(47,70,83,255))
for i=0,11 do local a=i*math.pi/6;line(im,196+math.cos(a)*27,61+math.sin(a)*27,196+math.cos(a)*36,61+math.sin(a)*36,1,rgba(75,93,95,255)) end
poly(im,{{196,24},{205,60},{196,97},{187,60}},rgba(28,43,58,255))
for _,x in ipairs({18,83,291,352}) do
  rect(im,x,12,14,142,rgba(26,39,53,255));rect(im,x,12,3,142,rgba(48,66,78,255));rect(im,x+9,13,2,139,rgba(15,25,38,255))
  rect(im,x-4,11,22,5,rgba(54,71,82,255));rect(im,x-5,150,24,6,rgba(56,72,82,255))
  for y=29,136,19 do rect(im,x+2,y,10,2,rgba(77,84,80,255)) end
  rect(im,x+4,68,3,15,rgba(57,132,149,255));rect(im,x+5,70,1,10,rgba(129,199,204,255))
end
rect(im,0,156,384,60,rgba(31,40,51,255));rect(im,0,156,384,2,rgba(118,127,132,255));rect(im,0,158,384,2,rgba(58,72,82,255))
for y=167,215,13 do line(im,0,y,383,y,1,rgba(18,27,39,255)) end
for x=0,383,26 do line(im,x,161,x-18,215,1,rgba(20,29,40,255)) end
for i=1,43 do local x=(i*97)%384;local y=160+(i*13)%54;rect(im,x,y,3+(i%6),1,rgba(58,69,77,255)) end
line(im,265,158,251,179,1,rgba(9,18,30,255));line(im,251,179,261,193,1,rgba(9,18,30,255))
b:newCel(b.layers[1],1,im,Point(0,0));b:saveAs(out .. '/CaptureRuins.aseprite');im:saveAs(out .. '/CaptureRuins.png');b:close()
local fx=Sprite(128,128,ColorMode.RGB);fx.layers[1].name='Pixel effect';index=0
local fxTimes={Slash={1,1,1,1,2,2,2,2},Parry={1,1,1,1,2,2,2,2},Kick={1,1,2,3,3,3,3,3},Vanish={3,3,3,3,3,3,3,3}}
for _,kind in ipairs({'Slash','Parry','Kick','Vanish'}) do local first=index+1;local sheet=Image(128*8,128,ColorMode.RGB)
  for f=1,8 do index=index+1;if index>1 then fx:newEmptyFrame(index) end;fx.frames[index].duration=math.floor(fxTimes[kind][f]/60*1000+.5)/1000
    local img=Image(128,128,ColorMode.RGB);local alpha=math.floor(255*(1-(f-1)/8));local gold=rgba(255,193,93,alpha);local white=rgba(255,246,213,alpha);local teal=rgba(107,203,212,alpha)
    if kind=='Slash' then
      for i=0,80 do local a=(-1.15+i/80*2.0);local r=16+f*2;local x=52+math.cos(a)*r;local y=64+math.sin(a)*r*.85
        if i<80-f*5 then rect(img,math.floor(x),math.floor(y),2,2,i>48 and white or teal) end
      end
      line(img,58,65,70+f*2,62-f,1,white)
    elseif kind=='Parry' then
      if f<=3 then ring(img,64,64,7+f*5,2,white);line(img,40-f*3,79+f*2,81+f*3,46-f*2,2,gold) end
      for k=0,7 do local a=k*math.pi/4+.3;local r=8+f*3;line(img,64+math.cos(a)*r,64+math.sin(a)*r,64+math.cos(a)*(r+8),64+math.sin(a)*(r+8),1,k%2==0 and gold or white) end
      if f==1 then rect(img,62,56,3,17,P.white);rect(img,56,62,17,3,P.white) end
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
    fx:newCel(fx.layers[1],index,img,Point(0,0));img:saveAs(out .. '/Fx_' .. kind .. '_' .. string.format('%02d',f) .. '.png');sheet:drawImage(img,Point((f-1)*128,0))
  end
  local tag=fx:newTag(first,index);tag.name=kind;sheet:saveAs(out .. '/Fx_' .. kind .. '.png')
end
fx:newLayer().name='Glow overlay (optional, runtime intensity)';fx:saveAs(out .. '/CombatEffects.aseprite');fx:close()

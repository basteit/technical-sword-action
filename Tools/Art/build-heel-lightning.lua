-- Pixel lightning lands at (64,128); floor wave is 47px below that contact.
local out=assert(app.params['out']);local rgba=app.pixelColor.rgba
local s=Sprite(128,192,ColorMode.RGB)
local bolt=s.layers[1];bolt.name='Pixel lightning - core and forks'
local wave=s:newLayer();wave.name='Pixel impact - hollow burst and floor wave'
local dust=s:newLayer();dust.name='Pixel fragments - outward and upward'
local glow=s:newLayer();glow.name='Optional glow guide (runtime)';glow.isVisible=false
local function px(im,x,y,c) x=math.floor(x+.5);y=math.floor(y+.5);if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,c) end end
local function line(im,a,b,w,c)
  local n=math.max(math.abs(a[1]-b[1]),math.abs(a[2]-b[2]),1)
  for i=0,n do local t=i/n;local x=math.floor(a[1]+(b[1]-a[1])*t+.5);local y=math.floor(a[2]+(b[2]-a[2])*t+.5)
    for yy=y-math.floor(w/2),y+math.floor(w/2) do for xx=x-math.floor(w/2),x+math.floor(w/2) do px(im,xx,yy,c) end end
  end
end
local function ring(im,cx,cy,r,sy,w,c)
  for y=math.floor(cy-r*sy-w),math.ceil(cy+r*sy+w) do for x=math.floor(cx-r-w),math.ceil(cx+r+w) do
    if math.abs(math.sqrt((x-cx)^2+((y-cy)/sy)^2)-r)<w/2 then px(im,x,y,c) end
  end end
end
local function outlined(im)
  local result=Image(im.spec)
  for y=0,im.height-1 do for x=0,im.width-1 do local c=im:getPixel(x,y);local a=app.pixelColor.rgbaA(c)
    if a>0 then for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do px(result,x+d[1],y+d[2],rgba(28,42,86,math.floor(a*.72))) end end
  end end
  result:drawImage(im);return result
end
local times={1,1,2,2,3,3,4,4};local sheet=Image(128*8,192,ColorMode.RGB)
for f=1,8 do
  if f>1 then s:newEmptyFrame(f) end;s.frames[f].duration=math.floor(times[f]/60*1000+.5)/1000
  local b,w,d=Image(s.spec),Image(s.spec),Image(s.spec)
  local alpha=math.floor(255*(1-(f-1)/9));local blue=rgba(55,103,224,alpha);local cyan=rgba(157,240,255,alpha);local white=rgba(249,253,255,alpha);local gold=rgba(242,190,86,alpha)
  if f<=4 then
    local p=f==2 and {{63,0},{70,23},{59,45},{67,67},{59,92},{68,110},{64,128}} or {{64,0},{57,23},{70,44},{61,66},{68,92},{61,111},{64,128}}
    for i=1,#p-1 do
      if f<=2 or i%2==f%2 then
        line(b,p[i],p[i+1],f<=2 and 5 or 1,blue)
        line(b,p[i],p[i+1],f==1 and 3 or 1,cyan)
        if f<=2 then line(b,p[i],p[i+1],1,white) end
      end
    end
    for _,q in ipairs({{{61,66},{45,79},{49,95},{33,111}},{{68,92},{86,77},{82,63},{96,47}},{{57,23},{37,34},{43,48}}}) do
      for i=1,#q-1 do line(b,q[i],q[i+1],1,f<=2 and cyan or blue) end
    end
  end
  if f<=2 then
    ring(w,64,128,5+f*2,.72,2,white)
    line(w,{64,118},{64,123},1,white);line(w,{64,133},{64,139},1,cyan)
  end
  for k=0,7 do local a=k*math.pi/4;local r=8+f*4;local length=math.max(2,15-f*2)
    if k%2==0 or f<=3 then line(w,{64+math.cos(a)*r,128+math.sin(a)*r*.65},{64+math.cos(a)*(r+length),128+math.sin(a)*(r+length)*.65},1,k%2==0 and cyan or white) end
  end
  ring(w,64,175,10+f*6,.22,f<=3 and 2 or 1,cyan)
  ring(w,64,175,14+f*6,.22,1,white)
  if f<=3 then line(w,{35-f*6,175},{93+f*6,175},1,blue) end
  for k=0,13 do
    local side=k%2==0 and 1 or -1;local t=f/8;local x=64+side*(7+k%7*4+f*3);local y=172-(10+k%5*5)*math.sin(t*math.pi)-f
    line(d,{x,y},{x+side*(k%3+1),y-2-k%3},k%4==0 and 2 or 1,k%3==0 and gold or cyan)
  end
  s:newCel(bolt,f,outlined(b));s:newCel(wave,f,outlined(w));s:newCel(dust,f,outlined(d))
  local im=Image(s.spec);im:drawSprite(s,f);im:saveAs(out..'/Fx_HeelLightning_'..string.format('%02d',f)..'.png');sheet:drawImage(im,Point((f-1)*128,0))
end
local tag=s:newTag(1,8);tag.name='HeelLightning'
s:saveAs(out..'/HeelDropEffects.aseprite');sheet:saveAs(out..'/Fx_HeelLightning.png');s:close()
local r=assert(app.open(out..'/HeelDropEffects.aseprite'));assert(#r.frames==8 and #r.layers==4 and #r.tags==1 and r.tags[1].name=='HeelLightning');r:close()

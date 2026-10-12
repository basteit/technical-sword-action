-- Author in Aseprite, keeping the user's source documents untouched.
local root=assert(app.params['root']); local out=assert(app.params['out'])
local idle=app.open(root .. '/Assets/Art/Player/Player_idle.aseprite')
local walk=app.open(root .. '/Assets/Art/Player/Player_walk.aseprite')
assert(idle.width==64 and idle.height==64 and walk.width==64 and walk.height==64)
local base=Image(idle.spec);base:drawSprite(idle,1)
local rgba=app.pixelColor.rgba
-- Seven flat character colours. Source pixels are used only in the hidden reference.
local C={outline=rgba(34,32,52,255),dark=rgba(52,49,72,255),hair=rgba(216,220,233,255),white=rgba(243,245,251,255),shade=rgba(164,169,191,255),skin=rgba(251,232,213,255),eye=rgba(62,79,183,255)}
C.steel=C.shade
local cyan=app.pixelColor.rgba(109,192,201,255)
local guideColor=app.pixelColor.rgba(228,155,83,255)
-- Rotate the drawn anatomy before rasterizing, rather than rotating a finished bitmap.
-- Each tumble key has its own knees, arms, coat and hair; the head/hip relation rolls too.
local drawingRoll=nil
local function pointOnCanvas(x,y)
  if not drawingRoll then return x,y end
  local dx,dy=x-32,y-32
  return 32+dx*drawingRoll.co-dy*drawingRoll.si,32+dx*drawingRoll.si+dy*drawingRoll.co
end
local function px(im,x,y,c) x=math.floor(x+.5);y=math.floor(y+.5);if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,c) end end
local function disk(im,x,y,r,c)
  x,y=pointOnCanvas(x,y)
  for yy=math.floor(y-r),math.ceil(y+r) do for xx=math.floor(x-r),math.ceil(x+r) do
    if (xx-x)^2+(yy-y)^2<=r*r then px(im,xx,yy,c) end
  end end
end
local function line(im,a,b,w,c)
  local d=math.max(math.abs(b[1]-a[1]),math.abs(b[2]-a[2]),1)
  for i=0,d do local t=i/d;disk(im,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t,w/2,c) end
end
local function poly(im,p,c)
  if drawingRoll then local transformed={};for _,v in ipairs(p) do local x,y=pointOnCanvas(v[1],v[2]);transformed[#transformed+1]={x,y} end;p=transformed end
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
local function head(im,hx,hy,angle,view,face)
  local co=math.cos(angle*math.pi/180);local si=math.sin(angle*math.pi/180)
  local function point(x,y) return {hx+face*x*co-y*si,hy+face*x*si+y*co} end
  local function shape(points,c) local t={};for _,p in ipairs(points) do t[#t+1]=point(p[1],p[2]) end;poly(im,t,c) end
  local function stroke(a,b,w,c) line(im,point(a[1],a[2]),point(b[1],b[2]),w,c) end
  -- Fresh silhouettes for front/three-quarter/profile/back; no cropped reference head.
  shape({{-8,-4},{-4,-9},{3,-9},{8,-5},{10,1},{8,7},{4,10},{-3,10},{-7,6}},C.outline)
  shape({{-7,-4},{-3,-8},{3,-8},{7,-4},{9,1},{7,6},{3,9},{-3,8},{-6,5}},C.hair)
  if view~='back' then
    local side=view=='side'
    shape(side and {{1,-3},{6,-2},{8,2},{9,4},{6,5},{5,8},{1,8},{-1,4}} or {{-4,-2},{3,-4},{7,0},{7,6},{3,9},{-2,8},{-4,5}},C.skin)
    if not side then stroke({view=='front' and -3 or 0,2},{view=='front' and -3 or 0,4},1,C.eye) end
    stroke({side and 6 or 5,2},{side and 6 or 5,4},1,C.eye)
    -- Large pale fringe and two cheek locks identify the original hairstyle.
    shape({{-7,-3},{-3,-8},{3,-8},{7,-4},{9,0},{8,4},{6,3},{4,-2},{1,1},{-1,-2},{-4,2},{-4,7},{-7,4}},C.hair)
    stroke({-5,-3},{-2,-6},1,C.shade)
    if side then stroke({6,2},{6,4},1,C.eye) end
  else
    shape({{-5,-5},{0,-8},{5,-5},{7,1},{5,8},{-2,9},{-6,4}},C.hair)
    stroke({-3,-4},{-4,5},1,C.shade)
  end
end
local function pose(im,weapon,p)
  if p.roll then local r=p.roll*math.pi/180;drawingRoll={co=math.cos(r),si=math.sin(r)} end
  local x=p.x or 32;local hy=p.hy or 15;local hip=p.hip or {x,39};local neck=p.neck or {p.hx or x,hy+10}
  local left=p.left or {{x-7,47},{x-9,56}};local right=p.right or {{x+4,48},{x+6,56}}
  local back=p.view=='back' or p.back;local face=p.face or 1
  local tail=p.tail or {neck[1]-15,neck[2]+18};local anchor={neck[1]-face*7,hy-5}
  -- A tapered curved ribbon stays connected even when the hair whips horizontally.
  local control={anchor[1]+(tail[1]-anchor[1])*.7,anchor[2]-8+(tail[2]-anchor[2])*.3}
  for pass=1,2 do for i=0,18 do local t=i/18;local u=1-t
    disk(im,u*u*anchor[1]+2*u*t*control[1]+t*t*tail[1],u*u*anchor[2]+2*u*t*control[2]+t*t*tail[2],(3-2*t)+(pass==1 and 1 or 0),pass==1 and C.outline or C.hair)
  end end
  limb(im,{hip[1]-3,hip[2]},left[1],left[2],3,C.dark)
  limb(im,{hip[1]+3,hip[2]},right[1],right[2],4,C.dark)
  line(im,{left[2][1]-2,left[2][2]},{left[2][1]+3,left[2][2]},3,C.outline)
  line(im,{right[2][1]-2,right[2][2]},{right[2][1]+4,right[2][2]},3,C.outline)
  local shoulder=p.view=='side' and 4 or 6
  local hem=p.hem or {hip[1]-11,hip[2]+14}
  poly(im,{{neck[1]-shoulder,neck[2]-1},{neck[1]+shoulder,neck[2]-1},{hip[1]+5,hip[2]},{hem[1]+6,hem[2]},{hem[1]-4,hem[2]+2},{hip[1]-7,hip[2]}},C.outline)
  poly(im,{{neck[1]-shoulder+1,neck[2]},{neck[1]+shoulder-1,neck[2]},{hip[1]+3,hip[2]-1},{hem[1]+4,hem[2]-1},{hem[1]-2,hem[2]},{hip[1]-5,hip[2]-1}},C.hair)
  -- Dark central tunic, light asymmetric coat and a plain belt; no shaded detail.
  if not back then poly(im,{{neck[1]-2,neck[2]+1},{neck[1]+2,neck[2]+1},{hip[1]+4,hip[2]},{hip[1]-1,hip[2]}},C.dark) end
  line(im,{hip[1]-5,hip[2]-4},{hip[1]+4,hip[2]-4},2,C.outline)
  local hand=p.hand or {x+11,34};local elbow=p.elbow or {x+7,29}
  limb(im,{neck[1]+shoulder,neck[2]+1},elbow,hand,3,C.hair)
  disk(im,hand[1],hand[2],1.5,C.skin)
  local lh=p.lhand or {x-7,36};limb(im,{neck[1]-shoulder,neck[2]+1},p.lelbow or {neck[1]-7,neck[2]+6},lh,3,C.hair);disk(im,lh[1],lh[2],1,C.skin)
  head(im,p.hx or x,hy,p.angle or 0,p.headView or (back and 'back' or p.view or 'three'),face)
  -- Sheath belongs to the body; blade motion is independently editable.
  line(im,{hip[1]-5,hip[2]-7},{hip[1]-15,hip[2]+11},2,C.outline)
  if p.tip then
    local dx=p.tip[1]-hand[1];local dy=p.tip[2]-hand[2];local len=math.max(math.sqrt(dx*dx+dy*dy),1)
    local hilt={hand[1]-dx/len*5,hand[2]-dy/len*5}
    line(weapon,hilt,hand,2,C.dark)
    line(weapon,{hand[1]-dy/len*3,hand[2]+dx/len*3},{hand[1]+dy/len*3,hand[2]-dx/len*3},2,C.steel)
    line(weapon,hand,p.tip,4,C.outline);line(weapon,hand,p.tip,2,C.steel);line(weapon,hand,p.tip,1,C.white)
  end
  if p.smear then
    for i=1,#p.smear-1 do line(weapon,p.smear[i],p.smear[i+1],1,cyan) end
  end
  drawingRoll=nil
  if p.arc then
    for i=1,#p.arc-1 do line(weapon,p.arc[i],p.arc[i+1],2,cyan);line(weapon,p.arc[i],p.arc[i+1],1,C.white) end
  end
end
local function settleReference(im,lower,lean)
  -- Newly drawn neutral pose, also used by IdleRough. No source pixel is copied.
  pose(im,Image(64,64,ColorMode.RGB),{hx=33+lean,hy=15+lower,hip={32,39+lower*.4},hand={40,39},elbow={40,32+lower},lhand={24,39},left={{27,48},{25,56}},right={{37,47},{40,56}}})
end
local actions={
 {name='VerticalSlash',frames={4,3,3,2,2,4,4,6},hit=10,branch=22,poses={
  {hx=28,hy=17,hip={30,40},angle=-12,hand={24,19},elbow={22,28},lhand={25,22},tip={7,6},tail={17,44},left={{24,48},{22,56}},right={{38,48},{42,56}}},
  {hx=27,hy=14,hip={29,40},angle=-15,view='side',hand={28,9},elbow={18,22},lhand={28,12},tip={9,2},tail={14,43},left={{22,48},{19,56}},right={{39,48},{45,56}}},
  {hx=32,hy=16,hip={31,41},angle=8,hand={44,16},elbow={43,26},lhand={42,20},tip={59,2},tail={12,33},left={{22,48},{16,56}},right={{42,48},{48,56}}},
  {hx=41,hy=25,hip={32,43},angle=25,view='side',hand={49,39},elbow={47,35},lhand={45,37},tip={57,61},tail={12,23},hem={18,43},left={{23,49},{15,56}},right={{44,49},{51,56}},smear={{48,5},{61,24},{61,48}}},
  {hx=41,hy=26,hip={33,43},angle=22,hand={48,43},lhand={44,40},tip={50,61},tail={16,24},hem={18,47},left={{24,49},{16,56}},right={{44,49},{51,56}}},
  {hx=39,hy=24,hip={33,42},angle=17,hand={45,42},tip={48,61},tail={18,35},left={{24,48},{18,56}},right={{43,49},{49,56}}},
  {hx=37,hy=21,hip={33,41},angle=12,hand={43,40},tip={52,57},left={{25,48},{20,56}},right={{42,49},{47,56}}},
  {hx=35,hy=19,hip={32,40},angle=7,hand={42,38},tip={54,54},left={{25,48},{21,56}},right={{40,48},{45,56}}}
 }},
 {name='HorizontalSlash',frames={3,3,3,2,2,3,4,6},hit=9,branch=20,poses={
  {hx=35,hy=19,hip={32,40},angle=7,hand={39,39},elbow={41,32},lhand={32,38},tip={48,55},tail={18,40},left={{25,48},{21,56}},right={{40,48},{45,56}}},
  {hx=25,hy=21,hip={29,42},angle=-17,view='back',hand={18,32},lhand={23,34},tip={1,29},tail={43,27},hem={42,49},left={{21,49},{16,56}},right={{39,49},{46,56}}},
  {hx=32,hy=21,hip={31,42},view='front',hand={40,34},lhand={34,35},tip={55,28},tail={14,32},hem={18,50},left={{22,49},{16,56}},right={{42,49},{48,56}}},
  {hx=40,hy=22,hip={33,42},angle=16,view='side',hand={49,33},elbow={46,33},lhand={42,35},tip={63,30},tail={12,27},hem={16,44},left={{24,49},{16,56}},right={{45,49},{52,56}},smear={{4,35},{22,29},{48,28},{63,30}}},
  {hx=38,hy=22,hip={34,42},angle=12,view='front',hand={47,36},tip={62,40},tail={14,22},hem={16,43},left={{25,49},{18,56}},right={{44,49},{51,56}}},
  {hx=35,hy=22,hip={34,42},angle=8,view='front',hand={44,39},elbow={42,33},tip={57,50},tail={12,31},hem={17,48},left={{26,49},{20,56}},right={{42,49},{48,56}}},
  {hx=33,hy=20,hip={33,41},view='three',hand={41,38},tip={54,52},tail={15,37},left={{25,49},{22,56}},right={{41,49},{46,56}}},
  {hx=34,hy=18,hip={32,40},hand={40,37},tip={52,53},left={{26,48},{22,56}},right={{39,48},{43,56}}}
 }},
 {name='RapidSheathe',frames={3,3,3,2,2,2,5,6,12},hit=11,branch=32,poses={
  {hx=34,hy=18,hip={32,40},hand={40,37},tip={52,53},tail={16,40},left={{26,48},{22,56}},right={{39,48},{43,56}}},
  {hx=36,hy=18,hip={32,41},angle=10,hand={44,24},tip={57,3},tail={12,29},left={{23,49},{17,56}},right={{42,49},{48,56}}},
  {hx=41,hy=24,hip={33,43},angle=22,view='side',hand={49,40},tip={59,60},tail={13,21},hem={15,45},left={{23,49},{16,56}},right={{44,49},{51,56}},smear={{38,2},{60,17},{63,49}}},
  {hx=29,hy=22,hip={32,43},angle=-12,view='back',hand={22,34},tip={1,31},tail={49,27},hem={48,45},left={{23,49},{16,56}},right={{44,49},{51,56}},smear={{4,31},{28,27},{63,34}}},
  {hx=38,hy=20,hip={33,42},angle=12,view='front',hand={46,28},tip={62,8},tail={14,37},hem={16,46},left={{23,49},{16,56}},right={{44,49},{51,56}},smear={{14,51},{39,27},{59,8}}},
  {hx=40,hy=22,hip={33,42},angle=17,hand={48,38},tip={61,55},tail={17,23},left={{24,49},{18,56}},right={{43,49},{49,56}}},
  {hx=35,hy=20,hip={32,41},hand={40,37},tip={59,44},left={{25,49},{20,56}},right={{41,49},{46,56}}},
  {hx=32,hy=21,hip={32,42},view='front',hand={31,38},tip={18,53},lhand={25,39},left={{25,49},{22,56}},right={{40,49},{45,56}}},
  {hx=30,hy=22,hip={31,42},angle=-7,hand={27,38},lhand={25,39},left={{24,49},{20,56}},right={{40,49},{46,56}}}
 }},
 {name='IaiSlash',frames={4,3,2,2,3,4,6,6},hit=7,branch=22,poses={
  {hx=30,hy=22,hip={31,42},angle=-7,hand={27,38},lhand={25,39},tail={19,44},left={{24,49},{20,56}},right={{40,49},{46,56}}},
  {hx=27,hy=25,hip={29,44},angle=-10,view='side',hand={29,38},lhand={25,40},tip={39,36},left={{21,49},{15,56}},right={{42,50},{49,56}}},
  {hx=43,hy=28,hip={32,44},angle=26,view='side',hand={50,34},elbow={47,39},lhand={22,43},tip={63,26},tail={9,26},hem={11,42},left={{22,49},{12,56}},right={{46,49},{55,56}},smear={{6,41},{27,35},{63,26}}},
  {hx=43,hy=27,hip={33,44},angle=24,hand={51,34},tip={63,25},tail={10,22},hem={11,42},left={{23,49},{13,56}},right={{46,49},{55,56}}},
  {hx=40,hy=24,hip={33,43},angle=18,view='front',hand={47,36},tip={63,35},tail={15,27},left={{23,49},{16,56}},right={{44,49},{51,56}}},
  {hx=36,hy=21,hip={32,42},angle=10,hand={40,37},tip={58,46},left={{24,49},{19,56}},right={{42,49},{48,56}}},
  {hx=33,hy=20,hip={32,41},hand={30,37},tip={20,50},lhand={25,38},left={{25,49},{21,56}},right={{40,49},{45,56}}},
  {hx=32,hy=18,hip={32,40},hand={27,36},lhand={25,38},left={{26,48},{22,56}},right={{39,48},{43,56}}}
 }},
 {name='ParryDeflect',frames={3,3,2,2,4,5},hit=6,branch=0,poses={
  {hx=32,hy=19,hip={32,42},hand={41,32},lhand={37,35},tip={49,10},left={{24,49},{20,56}},right={{41,49},{47,56}}},
  {hx=29,hy=21,hip={32,43},angle=-12,hand={41,31},lhand={36,34},tip={50,8},left={{23,50},{18,56}},right={{42,49},{49,56}}},
  {hx=25,hy=22,hip={32,44},angle=-20,view='side',hand={39,32},elbow={34,35},lhand={34,35},tip={48,9},tail={11,38},hem={19,51},left={{22,50},{16,56}},right={{42,50},{50,56}}},
  {hx=21,hy=26,hip={29,46},angle=-28,view='side',hand={34,35},elbow={28,38},lhand={30,38},tip={43,12},tail={7,30},hem={17,49},left={{19,51},{16,56}},right={{42,50},{50,56}}},
  {hx=36,hy=18,hip={33,42},angle=12,hand={48,24},elbow={42,31},lhand={40,29},tip={60,5},tail={14,33},hem={19,48},left={{25,49},{18,56}},right={{43,49},{49,56}},smear={{40,41},{51,26},{61,8}}},
  {hx=36,hy=17,hip={32,40},angle=8,hand={46,28},lhand={37,34},tip={57,11},tail={17,42},left={{25,48},{20,56}},right={{41,48},{46,56}}}
 }},
 {name='AirRoundhouse',frames={4,4,4,4,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2},hit=28,charge=18,release=24,landing=36,branch=36,recovery=36,poses={
  {hy=17,hip={32,42},hand={28,34},left={{25,49},{23,57}},right={{39,48},{40,57}}},
  {hy=16,hip={32,39},hand={28,32},left={{26,47},{23,55}},right={{39,44},{35,48}}},
  {hx=30,hy=13,hip={33,37},hand={29,29},left={{39,42},{42,49}},right={{26,39},{18,42}}},
  {roll=70,hx=32,hy=17,hip={32,34},hand={42,30},lhand={23,29},hem={20,38},left={{20,34},{23,44}},right={{41,41},{40,52}}},
  {roll=100,hx=32,hy=17,hip={32,34},hand={43,28},lhand={23,28},hem={20,36},left={{19,32},{22,42}},right={{40,42},{39,53}}},
  {roll=120,hx=32,hy=17,hip={32,34},angle=-20,view='side',hand={43,28},elbow={44,24},lhand={23,27},lelbow={19,24},tail={12,30},hem={20,36},left={{19,30},{20,40}},right={{40,43},{37,55}}},
  {roll=132,hx=32,hy=17,hip={32,34},angle=-18,view='side',hand={44,27},elbow={44,23},lhand={23,27},tail={12,28},hem={20,36},left={{19,29},{19,39}},right={{40,44},{36,55}}},
  {roll=144,hx=32,hy=17,hip={32,34},angle=-16,view='three',hand={44,26},elbow={44,23},lhand={23,26},tail={12,26},hem={20,35},left={{19,28},{18,38}},right={{39,44},{35,55}}},
  {roll=156,hx=32,hy=17,hip={32,34},angle=-14,view='three',hand={44,25},elbow={44,22},lhand={23,26},tail={12,24},hem={20,35},left={{20,27},{18,37}},right={{38,45},{34,55}}},
  {roll=168,hx=32,hy=17,hip={32,34},angle=-12,view='side',hand={44,25},elbow={44,22},lhand={23,25},tail={13,23},hem={20,34},left={{20,27},{18,36}},right={{38,45},{33,55}}},
  {roll=180,hx=32,hy=17,hip={32,34},angle=-10,view='side',hand={44,25},elbow={44,22},lhand={23,25},tail={13,22},hem={20,34},left={{20,26},{18,35}},right={{37,46},{32,56}}},
  {roll=205,hx=32,hy=17,hip={32,34},angle=-8,view='side',hand={44,25},elbow={44,22},lhand={23,24},tail={13,21},hem={20,33},left={{20,26},{17,35}},right={{35,46},{28,58}},arc={{27,5},{40,7},{49,13}}},
  {roll=235,hx=32,hy=17,hip={32,34},angle=-5,view='side',hand={44,25},elbow={44,22},lhand={23,24},tail={14,20},hem={20,32},left={{20,26},{17,35}},right={{32,47},{24,59}},arc={{34,3},{48,7},{58,19}}},
  {roll=260,hx=32,hy=17,hip={32,34},view='side',hand={44,25},elbow={44,22},lhand={23,24},tail={14,20},hem={20,32},left={{20,27},{17,36}},right={{33,46},{34,59}},arc={{42,5},{55,13},{61,28}}},
  {roll=280,hx=32,hy=17,hip={32,34},angle=3,view='side',hand={44,26},elbow={44,22},lhand={23,25},tail={14,20},hem={20,32},left={{20,28},{17,37}},right={{34,46},{32,60}},arc={{51,10},{60,24},{60,38}}},
  {roll=300,hx=32,hy=17,hip={32,34},angle=5,view='side',hand={44,28},elbow={44,23},lhand={23,26},tail={13,20},hem={20,33},left={{20,29},{17,38}},right={{36,45},{29,59}},arc={{60,23},{60,39},{54,50}}},
  {roll=320,hx=32,hy=18,hip={32,35},angle=8,view='three',hand={43,33},elbow={44,26},lhand={22,30},tail={13,20},hem={20,35},left={{22,36},{22,46}},right={{35,47},{27,57}},arc={{60,37},{53,50},{41,57}}},
  {roll=340,hx=32,hy=22,hip={32,40},angle=10,view='three',hand={43,47},elbow={44,35},lhand={23,42},tail={12,23},hem={20,40},left={{25,43},{22,50}},right={{39,47},{38,56}}},
  {hx=39,hy=34,neck={34,41},hip={28,46},angle=20,view='side',hand={48,56},elbow={45,46},lhand={27,49},tail={14,24},hem={17,44},left={{21,49},{15,56}},right={{43,47},{48,56}}}
 }}
}
-- Strong line-of-action keys: the head, shoulder line and hip move together.
actions[1].poses[2]={hx=24,hy=15,hip={28,41},angle=-22,view='side',hand={25,6},elbow={15,23},lhand={25,10},tip={4,2},tail={13,44},left={{20,49},{16,56}},right={{40,48},{49,56}}}
actions[1].poses[4]={hx=45,hy=30,neck={39,35},hip={30,44},angle=30,view='side',hand={49,42},elbow={44,35},lhand={45,40},tip={55,61},tail={9,20},hem={13,39},left={{22,48},{13,56}},right={{49,45},{52,56}},arc={{47,4},{59,17},{61,40},{56,59}}}
actions[1].poses[5]={hx=46,hy=32,neck={39,37},hip={31,45},angle=32,view='side',hand={48,45},elbow={44,38},lhand={44,43},tip={50,61},tail={10,22},hem={13,43},left={{22,49},{13,56}},right={{49,46},{52,56}}}
actions[1].poses[6]={hx=44,hy=31,neck={38,37},hip={31,44},angle=26,hand={46,44},elbow={43,38},lhand={40,41},tip={50,61},tail={13,24},hem={14,46},left={{23,49},{14,56}},right={{48,46},{52,56}}}
actions[1].poses[7]={hx=42,hy=29,neck={37,36},hip={31,43},angle=20,hand={44,42},lhand={36,40},tip={49,60},tail={14,29},hem={17,49},left={{23,49},{16,56}},right={{46,47},{50,56}}}
actions[1].poses[8]={hx=40,hy=27,neck={36,35},hip={32,43},angle=16,hand={42,42},lhand={33,40},tip={48,60},tail={16,34},left={{24,49},{18,56}},right={{45,48},{49,56}}}
actions[2].poses[1]=actions[1].poses[8]
actions[2].poses[2]={hx=23,hy=25,neck={28,33},hip={31,44},angle=-25,view='back',headView='side',hand={16,34},elbow={20,32},lhand={22,38},tip={1,23},tail={46,22},hem={44,45},left={{19,49},{16,56}},right={{43,49},{51,56}}}
actions[2].poses[3]={hx=32,hy=27,neck={32,36},hip={32,44},view='front',hand={34,35},elbow={39,34},lhand={29,36},tip={18,24},tail={17,23},hem={19,44},left={{23,49},{16,56}},right={{47,47},{52,56}},arc={{6,26},{28,20},{48,24}}}
actions[2].poses[4]={hx=44,hy=28,neck={39,34},hip={32,44},angle=22,view='side',hand={51,34},elbow={45,34},lhand={43,36},tip={63,29},tail={8,24},hem={12,40},left={{23,49},{14,56}},right={{49,46},{54,56}},arc={{5,30},{23,23},{46,24},{62,30}}}
actions[2].poses[5]={hx=44,hy=30,neck={39,36},hip={33,45},angle=22,view='front',hand={50,40},elbow={44,34},tip={62,46},tail={10,21},hem={13,41},left={{24,49},{16,56}},right={{49,47},{53,56}}}
actions[2].poses[6]={hx=42,hy=29,neck={37,36},hip={33,44},angle=18,hand={46,43},elbow={43,36},tip={56,57},tail={11,27},hem={15,45},left={{24,49},{17,56}},right={{47,47},{51,56}}}
actions[2].poses[7]={hx=40,hy=28,neck={36,35},hip={32,43},angle=14,hand={44,43},tip={54,59},tail={13,31},left={{24,49},{18,56}},right={{45,48},{49,56}}}
actions[2].poses[8]={hx=38,hy=26,neck={35,34},hip={32,43},angle=10,hand={42,42},tip={52,59},tail={15,35},left={{24,49},{18,56}},right={{44,48},{48,56}}}
actions[3].poses[1]=actions[2].poses[8]
actions[3].poses[3]=actions[1].poses[4]
actions[3].poses[4]=actions[2].poses[2]
actions[3].poses[6]=actions[2].poses[5]
actions[3].poses[8]={hx=28,hy=28,neck={30,37},hip={31,44},angle=-10,view='front',hand={29,42},tip={14,54},lhand={24,43},tail={15,35},left={{22,49},{17,56}},right={{43,49},{49,56}}}
actions[3].poses[9]={hx=26,hy=30,neck={29,39},hip={30,45},angle=-12,hand={27,44},lhand={25,44},tail={13,39},left={{21,50},{16,56}},right={{43,50},{49,56}}}
actions[4].poses[1]=actions[3].poses[9]
actions[4].poses[2]={hx=24,hy=32,neck={29,40},hip={29,46},angle=-16,view='side',hand={29,43},lhand={25,44},tip={37,41},tail={12,40},left={{20,51},{14,56}},right={{44,50},{51,56}}}
actions[4].poses[3]={hx=47,hy=35,neck={40,40},hip={28,46},angle=30,view='side',hand={54,36},elbow={45,40},lhand={20,45},tip={63,26},tail={7,29},hem={10,39},left={{20,50},{10,56}},right={{52,47},{56,56}},arc={{4,47},{25,37},{48,29},{62,27}}}
actions[4].poses[4]=actions[4].poses[3]
actions[4].poses[5]={hx=45,hy=33,neck={39,39},hip={29,45},angle=25,view='side',hand={51,36},elbow={44,39},tip={63,29},tail={9,24},hem={11,42},left={{21,50},{12,56}},right={{50,47},{55,56}}}
actions[4].poses[6]={hx=43,hy=30,neck={38,37},hip={31,44},angle=20,hand={45,40},tip={60,48},tail={12,27},left={{22,50},{15,56}},right={{47,48},{52,56}}}
actions[4].poses[7]={hx=41,hy=28,neck={36,36},hip={31,43},angle=16,hand={34,42},tip={19,55},lhand={26,43},tail={15,32},left={{23,49},{17,56}},right={{45,48},{49,56}}}
actions[4].poses[8]={hx=38,hy=26,neck={35,34},hip={32,43},angle=10,hand={29,41},lhand={26,42},tail={17,36},left={{24,49},{18,56}},right={{44,48},{48,56}}}
actions[5].poses[3]={hx=23,hy=29,neck={28,37},hip={34,46},angle=-30,view='side',hand={34,37},elbow={29,41},lhand={30,40},tip={44,12},tail={6,26},hem={20,48},left={{18,51},{15,56}},right={{43,50},{52,56}}}
actions[5].poses[4]={hx=20,hy=32,neck={26,39},hip={33,47},angle=-34,view='side',hand={31,39},elbow={26,42},lhand={28,42},tip={40,14},tail={5,23},hem={19,49},left={{17,52},{15,56}},right={{43,51},{52,56}}}
actions[5].poses[5]={hx=38,hy=19,neck={35,29},hip={31,42},angle=15,view='side',hand={48,20},elbow={43,28},lhand={42,26},tip={60,3},tail={10,34},hem={14,45},left={{23,49},{15,56}},right={{44,48},{51,56}},arc={{34,49},{47,35},{58,14}}}
actions[5].poses[6]={hx=39,hy=21,neck={35,30},hip={32,42},angle=12,hand={49,26},elbow={43,31},lhand={40,32},tip={61,10},tail={12,40},left={{24,49},{17,56}},right={{45,48},{50,56}}}
local recoveries={
 {name='Recovery_VerticalSlash',frames={4,4,4,4},hit=0,poses={
  actions[1].poses[8],{hx=37,hy=23,hip={32,42},angle=8,hand={40,40},tip={49,56},left={{25,49},{21,56}},right={{41,49},{45,56}}}, {settle=1,lean=0},{idle=true}}},
 {name='Recovery_HorizontalSlash',frames={4,4,4,4},hit=0,poses={
  actions[2].poses[8],{hx=36,hy=22,hip={32,42},angle=6,hand={40,39},tip={49,54},left={{25,49},{21,56}},right={{41,49},{45,56}}}, {settle=1,lean=0},{idle=true}}},
 {name='Recovery_RapidSheathe',frames={4,4,4,4},hit=0,poses={
  actions[3].poses[9],{hx=29,hy=24,hip={31,43},hand={29,40},lhand={25,41},left={{24,50},{20,56}},right={{41,49},{46,56}}}, {settle=1,lean=0},{idle=true}}},
 {name='Recovery_IaiSlash',frames={4,5,5,4},hit=0,poses={
  actions[4].poses[8],{hx=35,hy=22,hip={32,42},hand={29,39},lhand={25,40},left={{25,49},{21,56}},right={{41,49},{45,56}}}, {settle=1,lean=0},{idle=true}}},
 {name='Recovery_ParryDeflect',frames={3,4,4,4},hit=0,poses={
  actions[5].poses[6],{hand={37,36},tip={47,50}}, {settle=1,lean=0},{idle=true}}},
 {name='Recovery_AirRoundhouse',frames={5,5,5,5,5,4,4},hit=0,poses={
  {hx=39,hy=35,neck={34,42},hip={28,47},angle=22,view='side',hand={48,57},elbow={44,47},lhand={26,48},tail={13,27},hem={16,45},left={{20,50},{15,56}},right={{43,48},{48,56}}},
  {hx=40,hy=36,neck={35,43},hip={29,48},angle=20,view='side',hand={48,57},elbow={45,48},lhand={27,48},tail={14,34},hem={18,48},left={{21,51},{15,56}},right={{43,49},{48,56}}},
  {hx=38,hy=31,neck={34,39},hip={30,46},angle=14,hand={46,49},elbow={43,42},lhand={28,45},tail={17,43},left={{23,51},{18,56}},right={{42,49},{46,56}}},
  {hx=36,hy=26,neck={34,35},hip={31,43},angle=8,hand={43,44},elbow={41,37},lhand={27,42},tail={18,47},left={{25,50},{22,56}},right={{40,49},{44,56}}},
  {hy=20,hx=34,hip={32,41},angle=4,hand={41,41},elbow={40,34},lhand={25,40},tail={18,45},left={{26,49},{24,56}},right={{38,48},{42,56}}},
  {settle=2,lean=0},{idle=true}}},
 {name='Recovery_Move',frames={3,4,4},hit=0,poses={{walkStop=true},{settle=1,lean=0},{idle=true}}},
 {name='Recovery_Landing',frames={3,4,4},hit=0,poses={
  {hy=20,hip={32,42},hand={36,39},left={{27,50},{25,57}},right={{38,50},{40,57}}}, {settle=2,lean=0},{idle=true}}}
}
for _,a in ipairs(recoveries) do actions[#actions+1]=a end
-- Keep originals in the reference tags; use a matching, wholly redrawn rough idle/walk
-- in the capture so that recovery does not jump between two drawing constructions.
local neutral={name='IdleRough',frames={6,6,6,6,6,6},hit=0,poses={}}
for i=1,6 do neutral.poses[i]={settle=(i==3 or i==4) and 1 or 0} end
actions[#actions+1]=neutral
local move={name='WalkRough',frames={5,5,5,5,5,5,5,5},hit=0,poses={}}
for i=1,8 do local a=(i-1)*math.pi/4;local stride=math.sin(a)*8
  move.poses[i]={hx=35,hy=15+math.abs(math.cos(a)),hip={33,40},angle=5,hand={40,37-stride*.25},lhand={26,37+stride*.25},tail={18-stride*.3,41},hem={23-stride*.3,53},left={{29-stride*.5,48},{30-stride,56-math.max(0,math.cos(a))*3}},right={{35+stride*.5,48},{35+stride,56-math.max(0,-math.cos(a))*3}}}
end
actions[#actions+1]=move
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
    if a.poses[i].idle then settleReference(im,0,0)
    elseif a.poses[i].settle then settleReference(im,a.poses[i].settle,a.poses[i].lean or 0)
    elseif a.poses[i].walkStop then pose(im,wi,move.poses[8])
    else pose(im,wi,a.poses[i]) end
    s:newCel(rough,index,im,Point(0,0));s:newCel(blade,index,wi,Point(0,0))
    s:newCel(clean,index,Image(64,64,ColorMode.RGB),Point(0,0));s:newCel(cleanBlade,index,Image(64,64,ColorMode.RGB),Point(0,0))
    local gi=Image(64,64,ColorMode.RGB)
    for x=0,63,3 do px(gi,x,58,guideColor) end
    for y=0,63,3 do px(gi,32,y,cyan) end
    local hip=a.poses[i].hip or {32,40}
    if a.poses[i].roll then local r=a.poses[i].roll*math.pi/180;drawingRoll={co=math.cos(r),si=math.sin(r)} end
    disk(gi,hip[1],hip[2],2,guideColor)
    drawingRoll=nil
    local gc=s:newCel(guides,index,gi,Point(0,0));gc.data=a.name .. ' / ' .. i .. ' / ' .. n .. ' game frames; floor row 58; root pivot (32,38 bottom-origin)'
    im:drawImage(wi);sheet:drawImage(im,Point((i-1)*64,0))
    local name=a.name .. '_' .. string.format('%02d',i)
    im:saveAs(out .. '/' .. name .. '.png');exportEyes(im,name)
    local event='';if start==a.hit then event='Hit' end;if a.charge and start==a.charge then event='ChargeSlow' end
    if start==a.release then event='ReleaseSlow' end;if start==a.landing then event='Landing' end
    local phase=start<a.hit and 'anticipation' or start<a.hit+4 and 'active' or start<(a.branch or 999) and 'cancel-lock' or 'recovery'
    if a.release and start>=a.release and start<a.hit then phase='fast-swing' end
    if a.landing and start>=a.landing then phase=start<40 and 'ukemi' or 'stand' end
    if a.name:find('Recovery_')==1 then phase='free-recovery';event='' end
    if a.name=='IdleRough' or a.name=='WalkRough' then phase='loop';event='' end
    csv[#csv+1]=string.format('%s,%d,%d,%d,%d,%d,%s,%s',a.name,i,index,n,math.floor(n/60*1000+.5),start,phase,event)
    mf[#mf+1]=string.format('{"frames":%d,"start":%d}',n,start);start=start+n
  end
  local t=s:newTag(first,index);t.name=a.name
  tagBounds[#tagBounds+1]={name=a.name,first=first,last=index,tag=t}
  sheet:saveAs(out .. '/' .. a.name .. '.png')
  manifest[#manifest+1]=string.format('{"name":%q,"count":%d,"totalFrames":%d,"hitFrame":%d,"branchFrame":%d,"chargeFrame":%d,"releaseFrame":%d,"landingFrame":%d,"recoveryFrame":%d,"frames":[%s]}',a.name,#a.frames,start,a.hit,a.branch or 0,a.charge or 0,a.release or 0,a.landing or 0,a.recovery or a.branch or 0,table.concat(mf,','))
end
-- Aseprite extends a tag when a new frame is appended at its end. Restore all
-- intended ranges only after creating the entire document.
for _,b in ipairs(tagBounds) do b.tag.fromFrame=b.first;b.tag.toFrame=b.last end
local file=out .. '/Player_actions_rough.aseprite';s:saveAs(file);s:close()
local reopen=app.open(file)
assert(#reopen.layers==8 and #reopen.tags==18 and #reopen.frames==index)
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
local report=assert(io.open(out .. '/authoring-validation.json','w'));report:write(string.format('{"reopened":true,"tagRangesChecked":true,"layers":8,"tags":18,"frames":%d,"sourceFilesModified":false,"actionPixelsRedrawn":true,"referencePixelsOnlyInHiddenReference":true}',index));report:close()

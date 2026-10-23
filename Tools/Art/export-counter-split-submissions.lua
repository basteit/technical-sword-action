-- Export received player/VFX separately, preserving both original documents.
local out=assert(app.params['out']);local fxout=assert(app.params['fxout'])
local player=assert(app.open(assert(app.params['input'])))
local effect=assert(app.open(assert(app.params['effect'])))
assert(player.width==128 and player.height==128 and #player.frames==15)
assert(effect.width==256 and effect.height==256 and #effect.frames==15)
local ticks={2,1,1,1,1,1,1,1,1,4,4,4,4,4,3}
local function layer(s,name)for _,l in ipairs(s.layers)do if l.name==name then return l end end end
local body=assert(layer(player,'Body'));local reference=assert(layer(effect,'Body'))
for i=1,15 do
 local a=assert(body:cel(i));local b=assert(reference:cel(i))
 assert(b.position.x-a.position.x==89 and b.position.y-a.position.y==43,'VFX reference moved')
 assert(a.image.width==b.image.width and a.image.height==b.image.height)
 for p in a.image:pixels()do assert(p()==b.image:getPixel(p.x,p.y),'VFX body reference differs')end
end
local function write(path,value)local f=assert(io.open(path,'w'));f:write(json.encode(value));f:close()end
local actions,eyes={},{frames={}};local native={}
for _,entry in ipairs({{s=player,name='CounterKick',folder=out,pivotX=32,pivotY=38},{s=effect,name='CounterKickEffect',folder=fxout,pivotX=121,pivotY=123}}) do
 local s=entry.s;local isfx=s==effect;local frames={};local start=0;local sheet=Image(s.width*15,s.height,ColorMode.RGB)
 for _,l in ipairs(s.layers)do
  if isfx then l.isVisible=l.name=='Effect' or l.name=='Effect2'
  else l.isVisible=l.name=='Body' or l.name=='Hair' or l.name=='Eye' end
 end
 local durations={}
 for i=1,15 do
  local image=Image(s.spec);image:drawSprite(s,i);local file=entry.name..'_'..string.format('%02d',i)
  image:saveAs(entry.folder..'/'..file..'.png');sheet:drawImage(image,Point((i-1)*s.width,0))
  frames[#frames+1]={frames=ticks[i],start=start};start=start+ticks[i]
  durations[#durations+1]=math.floor(s.frames[i].duration*1000+.5)
  if not isfx then
   local mask=Image(s.spec);local count=0;local cel=layer(s,'Eye'):cel(i)
   if cel then for p in cel.image:pixels()do if app.pixelColor.rgbaA(p())>0 then mask:drawPixel(p.x+cel.position.x,p.y+cel.position.y,app.pixelColor.rgba(255,255,255,255));count=count+1 end end end
   mask:saveAs(out..'/EyeMasks/'..file..'.png');eyes.frames[#eyes.frames+1]={file=file,pixels=count}
  end
 end
 sheet:saveAs(entry.folder..'/'..entry.name..'.png')
 local action={name=entry.name,provisional=false,canvasWidth=s.width,canvasHeight=s.height,pivotX=entry.pivotX,pivotY=entry.pivotY,count=15,totalFrames=start,frames=frames}
 if not isfx then action.chargeFrame=0;action.releaseFrame=frames[8].start;action.hitFrame=frames[9].start;action.landingFrame=start;action.recoveryFrame=start;action.includesCounterTrail=true;action.counterContactOffset={x=51/32,y=4/32}end
 actions[#actions+1]=action;native[entry.name]=durations
end
write(out..'/timing-manifest.json',{actions=actions});write(out..'/eye-mask-manifest.json',eyes)
write(out..'/submission-validation.json',{sourceUnmodified=true,bodyReferenceMatchesAllFrames=true,bodyOffset={x=89,y=43},effectPivot={x=121,y=123},nativeDurations=native,pairedGameTicks=ticks,playerEffectExcluded=true,effectBodyAndEyeExcluded=true})
player:close();effect:close()

-- Reuse the reviewed impact silhouette with the submitted kick's cool palette.
-- The warm impact remains available for other attacks.
local folder=assert(app.params['folder'])
local first=Image{fromFile=folder..'/Fx_Kick_01.png'}
local s=Sprite(first.width,first.height,ColorMode.RGB);s.layers[1].name='Electric impact'
local times={1,1,2,3,3,3,3,3}
local sheet=Image(first.width*8,first.height,ColorMode.RGB)
for f=1,8 do
  if f>1 then s:newEmptyFrame() end
  local source=Image{fromFile=folder..'/Fx_Kick_'..string.format('%02d',f)..'.png'}
  local im=Image(s.spec)
  for p in source:pixels() do
    local c=p();local a=app.pixelColor.rgbaA(c)
    if a>0 then
      local l=.2126*app.pixelColor.rgbaR(c)+.7152*app.pixelColor.rgbaG(c)+.0722*app.pixelColor.rgbaB(c)
      local r,g,b=91,110,225
      if l>220 then r,g,b=244,252,255 elseif l>140 then r,g,b=186,243,255 end
      im:drawPixel(p.x,p.y,app.pixelColor.rgba(r,g,b,a))
    end
  end
  s:newCel(s.layers[1],f,im,Point(0,0));s.frames[f].duration=math.floor(times[f]/60*1000+.5)/1000
  im:saveAs(folder..'/Fx_ElectricKick_'..string.format('%02d',f)..'.png')
  sheet:drawImage(im,Point((f-1)*first.width,0))
end
local tag=s:newTag(1,8);tag.name='Fx_ElectricKick'
sheet:saveAs(folder..'/Fx_ElectricKick.png')
s:saveAs(folder..'/PlayerElectricKickImpact.aseprite');s:close()
local check=assert(app.open(folder..'/PlayerElectricKickImpact.aseprite'))
assert(#check.frames==8 and check.tags[1].name=='Fx_ElectricKick');check:close()

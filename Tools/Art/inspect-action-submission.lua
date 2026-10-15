-- Read-only inspection; writes flattened PNGs and metadata outside the source document.
local s=assert(app.open(assert(app.params['input'])))
local out=assert(app.params['out'])
local layers,tags,frames={},{},{}
for li,l in ipairs(s.layers) do
  layers[#layers+1]=string.format('{"name":%q,"visible":%s,"opacity":%d,"cels":%d}',l.name,tostring(l.isVisible),l.opacity,#l.cels)
  for _,c in ipairs(l.cels) do local im=Image(s.spec);im:drawImage(c.image,c.position);im:saveAs(out..'/layer-'..li..'-'..c.frame.frameNumber..'.png') end
end
for _,t in ipairs(s.tags) do tags[#tags+1]=string.format('{"name":%q,"first":%d,"last":%d}',t.name,t.fromFrame.frameNumber,t.toFrame.frameNumber) end
local sheet=Image(s.width*#s.frames,s.height,ColorMode.RGB)
for i,f in ipairs(s.frames) do
  local im=Image(s.spec);im:drawSprite(s,i)
  im:saveAs(out..'/frame-'..string.format('%02d',i)..'.png');sheet:drawImage(im,Point((i-1)*s.width,0))
  frames[#frames+1]=string.format('{"frame":%d,"ms":%d}',i,math.floor(f.duration*1000+.5))
end
sheet:saveAs(out..'/sheet.png')
local file=assert(io.open(out..'/inspection.json','w'))
file:write(string.format('{"width":%d,"height":%d,"layers":[%s],"tags":[%s],"frames":[%s]}',s.width,s.height,table.concat(layers,','),table.concat(tags,','),table.concat(frames,',')));file:close()
s:close()

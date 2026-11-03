-- Read the native document without changing visibility, timing, layers or tags.
local s=assert(app.open(assert(app.params['input'])))
local result={width=s.width,height=s.height,color_mode=s.colorMode,frames={},layers={},tags={}}
for i,f in ipairs(s.frames) do result.frames[#result.frames+1]={frame=i,duration_ms=math.floor(f.duration*1000+.5)} end
local function layers(items,parent)
  for _,l in ipairs(items) do
    local name=parent..l.name
    result.layers[#result.layers+1]={name=name,visible=l.isVisible,opacity=l.opacity,is_group=l.isGroup}
    if l.isGroup then layers(l.layers,name..'/') end
  end
end
layers(s.layers,'')
for _,t in ipairs(s.tags) do result.tags[#result.tags+1]={name=t.name,first=t.fromFrame.frameNumber,last=t.toFrame.frameNumber} end
local f=assert(io.open(assert(app.params['output']),'w'));f:write(json.encode(result));f:close()
s:close()

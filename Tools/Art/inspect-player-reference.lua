local root = assert(app.params['root'])
local out = assert(app.params['out'])
local results = {}
for _, action in ipairs({'idle','walk'}) do
  local s = app.open(root .. '/Assets/Art/Player/Player_' .. action .. '.aseprite')
  assert(s)
  local hiddenGuide = false
  if app.params['hide-guides'] == 'true' then
    for _, layer in ipairs(s.layers) do
      if layer.name == 'Layer 1' then
        -- The supplied files use this layer solely for the 64px black floor guide.
        -- Refuse to hide it if future artwork gives it any other content.
        for _, cel in ipairs(layer.cels) do
          assert(cel.position.x == 0 and cel.position.y == 58 and cel.image.width == 64 and cel.image.height == 1, 'Guide layer contains unexpected artwork')
          for p in cel.image:pixels() do
            local c = p()
            assert(app.pixelColor.rgbaR(c) == 0 and app.pixelColor.rgbaG(c) == 0 and app.pixelColor.rgbaB(c) == 0 and app.pixelColor.rgbaA(c) == 255, 'Guide layer is not a solid black line')
          end
        end
        hiddenGuide = layer.isVisible
        layer.isVisible = false
      end
    end
  end
  local frames = {}
  local colors = {}
  for i, f in ipairs(s.frames) do
    local im = Image(s.spec)
    im:drawSprite(s, f)
    local x0,y0,x1,y1 = s.width,s.height,-1,-1
    for p in im:pixels() do
      local c = p()
      if app.pixelColor.rgbaA(c) > 0 then
        x0=math.min(x0,p.x); y0=math.min(y0,p.y)
        x1=math.max(x1,p.x); y1=math.max(y1,p.y)
        colors[tostring(c)]=(colors[tostring(c)] or 0)+1
      end
    end
    im:saveAs(out .. '/Player_' .. action .. '_' .. string.format('%02d',i) .. '.png')
    frames[#frames+1]=string.format('{"frame":%d,"durationMs":%d,"bounds":[%d,%d,%d,%d]}',i,math.floor(f.duration*1000+.5),x0,y0,x1,y1)
  end
  local layers={};for _,l in ipairs(s.layers) do layers[#layers+1]=string.format('%q',l.name) end
  local tags={};for _,t in ipairs(s.tags) do tags[#tags+1]=string.format('{"name":%q,"from":%d,"to":%d}',t.name,t.fromFrame.frameNumber,t.toFrame.frameNumber) end
  local cs={};for c,n in pairs(colors) do cs[#cs+1]=string.format('{"rgba":%s,"pixels":%d}',c,n) end
  results[#results+1]=string.format('{"action":%q,"width":%d,"height":%d,"hiddenFloorGuide":%s,"layers":[%s],"tags":[%s],"frames":[%s],"colors":[%s]}',action,s.width,s.height,tostring(hiddenGuide),table.concat(layers,','),table.concat(tags,','),table.concat(frames,','),table.concat(cs,','))
  s:close()
end
local f=assert(io.open(out .. '/reference-audit.json','w'));f:write('[' .. table.concat(results,',') .. ']');f:close()

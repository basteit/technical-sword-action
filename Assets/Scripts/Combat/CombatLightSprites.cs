using System.Collections.Generic;
using UnityEngine;

/// <summary>Full-rect lighting copies for sliced runtime sprites; never reimports source artwork.</summary>
internal sealed class CombatLightSprites
{
    private readonly Dictionary<Sprite,Sprite> sized = new();
    public Sprite For(SpriteRenderer renderer)
    {
        var source=renderer.sprite;
        if(source==null||renderer.drawMode==SpriteDrawMode.Simple)return source;
        if(sized.TryGetValue(source,out var sprite))return sprite;
        var rect=source.packed?source.textureRect:source.rect;
        sprite=Sprite.Create(source.texture,rect,new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height),
            source.pixelsPerUnit,0,SpriteMeshType.FullRect,source.border);
        sprite.name=source.name+"_LightFullRect";sprite.hideFlags=HideFlags.DontSave;sized.Add(source,sprite);
        return sprite;
    }
    public void Clear()
    {foreach(var sprite in sized.Values)if(sprite!=null)Object.Destroy(sprite);sized.Clear();}
}

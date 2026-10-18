using UnityEngine;

/// <summary>Moves/resizes the drawing, never the body or its collision/hit origins.</summary>
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(12000)]
public sealed class PrototypePlayerArtPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer source;
    [SerializeField] private SpriteRenderer artwork;
    [SerializeField] private BoxCollider2D body;
    [SerializeField] private PrototypePlayerArtSet artSet;
    private bool originalForceRenderingOff;
    private bool ownsVisibility;

    public SpriteRenderer Artwork => artwork;
    public PrototypePlayerArtSet ArtSet => artSet;

    public void Configure(SpriteRenderer sourceRenderer, SpriteRenderer artworkRenderer, BoxCollider2D collider, PrototypePlayerArtSet set)
    {
        ReleaseVisibility();
        source = sourceRenderer; artwork = artworkRenderer; body = collider; artSet = set;
        RefreshVisual();
    }

    private void OnEnable() => RefreshVisual();
    private void LateUpdate() => RefreshVisual();

    public void RefreshVisual()
    {
        if (source == null || artwork == null || source == artwork || body == null || artSet == null) return;
        if (!ownsVisibility)
        {
            originalForceRenderingOff = source.forceRenderingOff;
            ownsVisibility = true;
        }
        source.forceRenderingOff = true;
        artwork.enabled = source.enabled;
        artwork.sprite = source.sprite;
        artwork.color = source.color;
        artwork.flipX = source.flipX; artwork.flipY = source.flipY;
        artwork.sortingLayerID = source.sortingLayerID;
        artwork.sortingOrder = source.sortingOrder;
        artwork.sharedMaterial = source.sharedMaterial;
        if (source.sprite == null) return;

        // Some unmodified Aseprite samples are 100 PPU with a bottom pivot.
        // Normalize their displayed pixels without reimporting or editing the artist's files.
        float scale = source.sprite.pixelsPerUnit / Mathf.Max(1f, artSet.pixelsPerUnit);
        artwork.transform.localScale = new Vector3(scale, scale, 1f);
        float feetY = body.offset.y - body.size.y * .5f;
        artwork.transform.localPosition = new Vector3(0,
            feetY + (artSet.IsFeetPivot(source.sprite) ? 0 : artSet.commonFeetBelowPivot), 0);
    }

    private void OnDisable()
    {
        ReleaseVisibility();
        if (artwork != null) artwork.enabled = false;
    }

    private void ReleaseVisibility()
    {
        if (ownsVisibility && source != null) source.forceRenderingOff = originalForceRenderingOff;
        ownsVisibility = false;
    }
}

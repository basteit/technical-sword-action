using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Additional gameplay sparks/light. Procedural geometry, not recolored or edited received artwork.</summary>
public sealed class ParryOrangeImpact : MonoBehaviour
{
    private const int SparkCount = 32, RingSegments = 24, FlareRayCount = 3;
    private static readonly float[] Angles = { 18, 38, 61, 85, 112, 142, 165, 195, 218, 247, 278, 306, 330, 350 };
    private Camera captureCamera;
    private CounterCinematicProfile profile;
    private MeshRenderer sparks, glow, flare;
    private Mesh sparkMesh, glowMesh, flareMesh;
    private Material sparkMaterial, glowMaterial;
    private readonly Vector3[] sparkVertices = new Vector3[SparkCount * 12];
    private readonly Color[] sparkColors = new Color[SparkCount * 12];
    private readonly Vector3[] glowVertices = new Vector3[1 + RingSegments * 2];
    private readonly Color[] glowColors = new Color[1 + RingSegments * 2];
    private readonly Vector3[] flareVertices = new Vector3[FlareRayCount * 8];
    private readonly Color[] flareColors = new Color[FlareRayCount * 8];
    private float age = -1;
    private int facing;
    private bool fresh, glowSuppressed;
    public Vector2 WorldCenter { get; private set; }
    public bool IsSparkVisible { get; private set; }
    public bool IsGlowVisible { get; private set; }
    public bool IsFlareVisible { get; private set; }
    public int ParticleCount => age >= 0 && profile != null ? profile.parryReferenceAccent ? SparkCount : Angles.Length : 0;
    public int ActiveRendererCount => (IsSparkVisible ? 1 : 0) + (IsGlowVisible ? 1 : 0) + (IsFlareVisible ? 1 : 0);
    public int EmissionCount { get; private set; }
    public float Elapsed => age;

    public void Configure(Camera camera, Material additive)
    {
        captureCamera = camera;
        sparkMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
        glowMaterial = new Material(additive) { hideFlags = HideFlags.DontSave };
        sparkMaterial.mainTexture = glowMaterial.mainTexture = Texture2D.whiteTexture;
        sparkMaterial.color = glowMaterial.color = Color.white;
        sparks = CreateRenderer("ParryOrangeSparks", sparkMaterial, 1054, out sparkMesh);
        glow = CreateRenderer("ParryOrangeGlow", glowMaterial, 1051, out glowMesh);
        flare = CreateRenderer("ParryReferenceFlare", glowMaterial, 1055, out flareMesh);
        int[] triangles = new int[SparkCount * 18];
        for (int q = 0; q < SparkCount * 3; q++)
        {
            int v = q * 4, t = q * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        PrepareMesh(sparkMesh, sparkVertices, triangles);
        int[] radial = new int[RingSegments * 9];
        for (int s = 0; s < RingSegments; s++)
        {
            int next = (s + 1) % RingSegments, i = 1 + s, n = 1 + next, o = i + RingSegments, on = n + RingSegments, t = s * 9;
            radial[t] = 0; radial[t + 1] = i; radial[t + 2] = n;
            radial[t + 3] = i; radial[t + 4] = o; radial[t + 5] = on;
            radial[t + 6] = i; radial[t + 7] = on; radial[t + 8] = n;
        }
        PrepareMesh(glowMesh, glowVertices, radial);
        var flareTriangles = new int[FlareRayCount * 12];
        for (int q = 0; q < FlareRayCount * 2; q++)
        {
            int v = q * 4, t = q * 6;
            flareTriangles[t] = v; flareTriangles[t + 1] = v + 1; flareTriangles[t + 2] = v + 2;
            flareTriangles[t + 3] = v; flareTriangles[t + 4] = v + 2; flareTriangles[t + 5] = v + 3;
        }
        PrepareMesh(flareMesh, flareVertices, flareTriangles);
    }
    private MeshRenderer CreateRenderer(string name, Material material, int order, out Mesh mesh)
    {
        var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
        root.transform.SetParent(transform, false);
        mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.sortingOrder = order; renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return renderer;
    }
    private static void PrepareMesh(Mesh mesh, Vector3[] vertices, int[] indices)
    {
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < uv.Length; i++) uv[i] = Vector2.one * .5f;
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
    }
    public void Begin(Vector2 contact, int direction, CounterCinematicProfile tuning)
    {
        ResetAccent();
        if (tuning == null || !tuning.parryOrangeAccent || !tuning.IsParryTuningValid ||
            !float.IsFinite(contact.x) || !float.IsFinite(contact.y) || sparks == null || glow == null) return;
        profile = tuning; WorldCenter = contact; facing = direction < 0 ? -1 : 1; age = 0; fresh = true; EmissionCount++;
    }
    public void Advance(float seconds, float strength, float flash)
    {
        if (age < 0 || profile == null) return;
        if (!float.IsFinite(seconds) || seconds < 0 || !float.IsFinite(strength) || !float.IsFinite(flash)) return;
        if (!profile.parryOrangeAccent || !profile.IsParryTuningValid || strength <= 0) { ResetAccent(); return; }
        if (flash <= 0) glowSuppressed = true;
        if (!CombatTimeController.IsPaused) { if (!fresh) age += seconds; fresh = false; }
        if (age >= Mathf.Max(profile.parrySparkSeconds, Mathf.Max(profile.parryGlowSeconds, profile.parryReferenceAccent ? profile.parryFlareSeconds : 0))) { ResetAccent(); return; }
        IsSparkVisible = age < profile.parrySparkSeconds;
        IsGlowVisible = age < profile.parryGlowSeconds && flash > 0 && !glowSuppressed;
        IsFlareVisible = profile.parryReferenceAccent && age < profile.parryFlareSeconds && flash > 0 && !glowSuppressed;
        DrawSparks(Mathf.Clamp01(strength)); DrawGlow(Mathf.Clamp01(strength) * Mathf.Clamp01(flash));
        DrawFlare(Mathf.Clamp01(strength) * Mathf.Clamp01(flash));
    }
    private void DrawSparks(float strength)
    {
        float progress = Mathf.Clamp01(age / profile.parrySparkSeconds), fade = (1 - progress) * (1 - progress);
        float travel = 1 - (1 - progress) * (1 - progress);
        for (int i = 0; i < SparkCount; i++)
        {
            if (!profile.parryReferenceAccent && i >= Angles.Length)
            {
                for (int j = 0; j < 12; j++) { sparkVertices[i * 12 + j] = Vector3.zero; sparkColors[i * 12 + j] = Color.clear; }
                continue;
            }
            float angle = (profile.parryReferenceAccent ? (i * 137.50776f + 18) % 360 : Angles[i]) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));
            float radius = profile.parryReferenceAccent ? profile.parryReferenceSparkRadius : profile.parrySparkRadius;
            float distance = .08f + radius * travel * (.55f + (i % 5) * .11f);
            Vector2 head = direction * distance + Vector2.down * (.24f * progress * progress);
            float length = profile.parryReferenceAccent ? .12f + .8f * (1 - progress) * (.45f + ((i * 7) % 11) * .075f) :
                .08f + .18f * (1 - progress) * (.6f + (i % 3) * .2f);
            Vector2 tail = head - direction * length;
            var tint = profile.parrySparkColor; tint.a *= fade * strength;
            SetQuad(i * 12, tail, head, 3f / 32, new Color(.32f, .055f, .008f, tint.a * .85f));
            SetQuad(i * 12 + 4, tail, head, 1f / 32, tint);
            SetQuad(i * 12 + 8, Vector2.Lerp(tail, head, .72f), head, 1f / 32, new Color(1, .88f, .38f, tint.a));
        }
        sparkMesh.SetVertices(sparkVertices); sparkMesh.SetColors(sparkColors); sparkMesh.RecalculateBounds();
    }
    private void SetQuad(int start, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 delta = (b - a).normalized, side = new Vector2(-delta.y, delta.x) * width * .5f;
        sparkVertices[start] = Snap(a - side); sparkVertices[start + 1] = Snap(a + side);
        sparkVertices[start + 2] = Snap(b + side); sparkVertices[start + 3] = Snap(b - side);
        for (int i = 0; i < 4; i++) sparkColors[start + i] = tint;
    }
    private static Vector3 Snap(Vector2 point) => new Vector3(Mathf.Round(point.x * 64) / 64, Mathf.Round(point.y * 64) / 64, 0);
    private void DrawGlow(float strength)
    {
        float progress = Mathf.Clamp01(age / profile.parryGlowSeconds);
        Color color = profile.parryGlowColor; color.a *= strength * (1 - progress) * (1 - progress);
        color.r *= profile.parryGlowGain; color.g *= profile.parryGlowGain; color.b *= profile.parryGlowGain;
        float radius = profile.parryGlowRadius * Mathf.Lerp(.6f, 1, progress);
        glowVertices[0] = Vector3.zero; glowColors[0] = color;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = 2 * Mathf.PI * i / RingSegments;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .75f, 0);
            glowVertices[1 + i] = direction * radius * .4f; glowColors[1 + i] = new Color(color.r, color.g, color.b, color.a * .24f);
            glowVertices[1 + RingSegments + i] = direction * radius;
            glowColors[1 + RingSegments + i] = new Color(color.r, color.g, color.b, 0);
        }
        glowMesh.SetVertices(glowVertices); glowMesh.SetColors(glowColors); glowMesh.RecalculateBounds();
    }
    private void DrawFlare(float strength)
    {
        float p = Mathf.Clamp01(age / profile.parryFlareSeconds), fade = (1 - p) * (1 - p) * strength;
        for (int i = 0; i < FlareRayCount; i++)
        {
            float angle = (i == 0 ? 0 : i == 1 ? -25 : 65) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));
            float length = profile.parryFlareLength * (i == 0 ? 1 : i == 1 ? .55f : .3f) * Mathf.Lerp(.7f, 1, 1 - (1 - p) * (1 - p));
            SetFlareDiamond(i * 8, direction, length, i == 0 ? .2f : .12f, new Color(3, 1.1f, .12f, fade * .65f));
            SetFlareDiamond(i * 8 + 4, direction, length * .85f, 1f / 32, new Color(4, 3.2f, 1.6f, fade * .9f));
        }
        flareMesh.SetVertices(flareVertices); flareMesh.SetColors(flareColors); flareMesh.RecalculateBounds();
    }
    private void SetFlareDiamond(int start, Vector2 direction, float length, float width, Color color)
    {
        Vector2 side = new Vector2(-direction.y, direction.x) * width * .5f;
        flareVertices[start] = Snap(-direction * length * .5f); flareVertices[start + 1] = Snap(side);
        flareVertices[start + 2] = Snap(direction * length * .5f); flareVertices[start + 3] = Snap(-side);
        flareColors[start] = flareColors[start + 2] = new Color(color.r, color.g, color.b, 0);
        flareColors[start + 1] = flareColors[start + 3] = color;
    }
    public void ResetAccent()
    { age = -1; profile = null; glowSuppressed = false; IsSparkVisible = IsGlowVisible = IsFlareVisible = false; SetRenderers(false); }
    private void OnEnable()
    { RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; }
    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == captureCamera && age >= 0)
        {
            var position = new Vector3(WorldCenter.x, WorldCenter.y, 0);
            sparks.transform.SetPositionAndRotation(position, Quaternion.identity);
            glow.transform.SetPositionAndRotation(position, Quaternion.identity);
            flare.transform.SetPositionAndRotation(position, Quaternion.identity);
            SetRenderers(true);
        }
        else SetRenderers(false);
    }
    private void AfterCamera(ScriptableRenderContext context, Camera camera) => SetRenderers(false);
    private void SetRenderers(bool visible)
    { if (sparks != null) sparks.enabled = visible && IsSparkVisible; if (glow != null) glow.enabled = visible && IsGlowVisible; if (flare != null) flare.enabled = visible && IsFlareVisible; }
    private void OnDisable()
    { RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera; ResetAccent(); }
    private void OnDestroy()
    {
        if (sparks != null) Dispose(sparks.gameObject); if (glow != null) Dispose(glow.gameObject);
        if (flare != null) Dispose(flare.gameObject);
        Dispose(sparkMesh); Dispose(glowMesh); Dispose(flareMesh); Dispose(sparkMaterial); Dispose(glowMaterial);
    }
    private static void Dispose(Object value)
    { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
}

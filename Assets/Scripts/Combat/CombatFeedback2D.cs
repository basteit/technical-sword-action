using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in encounter presentation. Filming/legacy scenes keep their existing owners.</summary>
[DefaultExecutionOrder(11000)]
public sealed class CombatFeedback2D : MonoBehaviour
{
    private sealed class Effect
    {
        public GameObject root;
        public LineRenderer core, accent;
        public CombatFeedbackKind kind;
        public Vector2 position;
        public float age, duration, size, decoration;
        public Color tint;
    }
    private sealed class Flash
    {
        public SpriteRenderer renderer;
        public Color original, tint;
        public float remaining;
    }
    [SerializeField] private CombatFeedbackProfile profile;
    [SerializeField] private CombatFeedbackSettings settings;
    [SerializeField] private Camera feedbackCamera;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Damageable2D[] enemies = new Damageable2D[0];
    [SerializeField, Range(4, 64)] private int poolCapacity = 24;
    private readonly List<Effect> pool = new();
    private readonly List<Flash> flashes = new();
    private readonly Dictionary<AudioSource, float> audioVolumes = new();
    private PlayerAttack2D attack;
    private PlayerParryCounter2D counter;
    private PlayerParry2D parry;
    private PlayerDamageReceiver2D health;
    private PlayerSpecialSkill2D special;
    private AudioSource sound;
    private CombatFeedbackSettings runtimeSettings;
    private Vector3 baseCameraPosition;
    private Vector3 appliedShake;
    private UnityEngine.Object cameraFocusOwner;
    private Transform cameraSubject;
    private Vector2 cameraFocusOffset;
    private float baseCameraSize, cameraFocusSize, cameraFocusBlend, cameraBlendSeconds;
    private bool hasCameraFocus, cameraFocusRequested;
    private float shakeAge, shakeDuration, shakeMagnitude;
    private long lastAudioTick = -1;
    private int lastAudioPriority = -1;
    private Material ownedMaterial;
    private CombatFeedbackProfile validatedProfile;
    private bool profileValid;
    public CombatFeedbackSettings Settings => runtimeSettings != null ? runtimeSettings : settings;
    public bool IsReady
    {
        get
        {
            if (profile != validatedProfile)
            { validatedProfile = profile; profileValid = profile != null && profile.IsValid; }
            return isActiveAndEnabled && profileValid && Settings != null;
        }
    }
    public int EmissionCount { get; private set; }
    public int EssentialGlyphCount { get; private set; }
    public int ActiveEffectCount { get { int n = 0; foreach (var e in pool) if (e.root.activeSelf) n++; return n; } }
    public int ActiveFlashCount => flashes.Count;
    public CombatFeedbackKind LastKind { get; private set; }
    public long LastEmissionTick { get; private set; }
    public Vector3 ShakeOffset => appliedShake;
    public bool HasCameraFocus => hasCameraFocus;
    public float CameraFocusBlend => cameraFocusBlend;
    public static bool IsManaging(GameObject target)
    {
        var feedback = target != null ? target.GetComponent<CombatFeedback2D>() : null;
        return feedback != null && feedback.IsReady;
    }

    private void Awake()
    {
        attack = GetComponent<PlayerAttack2D>(); counter = GetComponent<PlayerParryCounter2D>();
        parry = GetComponent<PlayerParry2D>(); health = GetComponent<PlayerDamageReceiver2D>();
        special = GetComponent<PlayerSpecialSkill2D>();
        sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0;
        if (settings != null) runtimeSettings = Instantiate(settings);
        if (feedbackCamera == null) feedbackCamera = Camera.main;
        if (feedbackCamera != null) baseCameraPosition = feedbackCamera.transform.position;
        if (lineMaterial == null)
        {
            ownedMaterial = new Material(Shader.Find("Sprites/Default")); lineMaterial = ownedMaterial;
        }
        RememberAudio(gameObject);
        foreach (var enemy in enemies) if (enemy != null) RememberAudio(enemy.gameObject);
        for (int i = 0; i < Mathf.Clamp(poolCapacity, 4, 64); i++) pool.Add(CreateEffect(i));
    }
    private void RememberAudio(GameObject root)
    {
        foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
            if (!audioVolumes.ContainsKey(audio)) audioVolumes.Add(audio, audio.volume);
    }
    private void OnEnable()
    {
        if (attack != null) attack.HitConfirmed += OnAttack;
        if (counter != null) counter.HitConfirmed += OnCounter;
        if (parry != null) { parry.Succeeded += OnParry; parry.Failed += OnParryMiss; }
        if (health != null) health.DamageReceived += OnPlayerHit;
        if (special != null) special.HitConfirmed += OnSpecial;
        foreach (var enemy in enemies)
            if (enemy != null) { enemy.Died += OnDefeat; enemy.SetFeedbackOwner(this); }
        CombatTimeController.SessionReset += ResetPresentation;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }
    private void OnDisable()
    {
        if (attack != null) attack.HitConfirmed -= OnAttack;
        if (counter != null) counter.HitConfirmed -= OnCounter;
        if (parry != null) { parry.Succeeded -= OnParry; parry.Failed -= OnParryMiss; }
        if (health != null) health.DamageReceived -= OnPlayerHit;
        if (special != null) special.HitConfirmed -= OnSpecial;
        foreach (var enemy in enemies)
            if (enemy != null) { enemy.Died -= OnDefeat; enemy.ReleaseFeedbackOwner(this); }
        CombatTimeController.SessionReset -= ResetPresentation;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        ResetPresentation();
        foreach (var pair in audioVolumes) if (pair.Key != null) pair.Key.volume = pair.Value;
    }
    private void OnDestroy()
    {
        if (runtimeSettings != null) Destroy(runtimeSettings);
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
    private void OnSceneChanged(Scene before, Scene after) => ResetPresentation();
    private void OnAttack(Damageable2D target, int step, Vector2 position) =>
        Present(step == 4 ? CombatFeedbackKind.HeavyHit : CombatFeedbackKind.LightHit, position, target);
    private void OnCounter(Damageable2D target, Vector2 position) => Present(CombatFeedbackKind.Counter, position, target);
    private void OnParry(Damageable2D target, ParryResult result) =>
        Present(result == ParryResult.Just ? CombatFeedbackKind.JustParry : CombatFeedbackKind.Parry,
            target != null ? ((Vector2)target.transform.position + (Vector2)transform.position) * .5f : (Vector2)transform.position, null);
    private void OnParryMiss() => Present(CombatFeedbackKind.ParryMiss, transform.position, null);
    private void OnPlayerHit(int amount, string source, long sequence) => Present(CombatFeedbackKind.PlayerHit, transform.position, null);
    private void OnSpecial(Damageable2D target, int amount) => Present(CombatFeedbackKind.HeavyHit, target.transform.position, target);
    private void OnDefeat(Damageable2D target, Vector2 direction) => Present(CombatFeedbackKind.Defeat, target.transform.position, null);

    public void Present(CombatFeedbackKind kind, Vector2 position, Damageable2D target)
    {
        if (!IsReady || CombatTimeController.IsPaused) return;
        var entry = profile.Find(kind);
        LastKind = kind; LastEmissionTick = CombatTimeController.TickCount;
        EmissionCount++; EssentialGlyphCount++;
        if (entry.hitstopFrames > 0) CombatTimeController.RequestHitstop(this, entry.hitstopFrames / 60f);
        float magnitude = entry.shakePixels / 32f * Mathf.Clamp01(Settings.shake);
        if (magnitude > 0)
        {
            shakeAge = 0; shakeDuration = Mathf.Max(shakeDuration, entry.shakeSeconds);
            shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
        }
        var effect = pool.Find(x => !x.root.activeSelf) ?? pool[EmissionCount % pool.Count];
        effect.kind = kind; effect.position = position; effect.age = 0;
        effect.duration = entry.lifetimeFrames / 60f; effect.size = entry.glyphSize;
        effect.decoration = Mathf.Clamp01(Settings.vfx); effect.tint = entry.tint;
        effect.root.SetActive(true); DrawEffect(effect);
        SpriteRenderer sprite = kind == CombatFeedbackKind.PlayerHit ? GetComponentInChildren<SpriteRenderer>() :
            target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        if (sprite != null && entry.flashFrames > 0 && Settings.flash > 0)
        {
            var flash = flashes.Find(x => x.renderer == sprite);
            if (flash == null) { flash = new Flash { renderer = sprite, original = sprite.color }; flashes.Add(flash); }
            flash.tint = Color.Lerp(flash.original, entry.tint, Mathf.Clamp01(Settings.flash));
            flash.remaining = Mathf.Max(flash.remaining, entry.flashFrames / 60f); sprite.color = flash.tint;
        }
        // At most one voice for a combat tick; upgrade to the stronger cue rather than stacking/clipping.
        int priority = kind == CombatFeedbackKind.JustParry ? 10 : kind == CombatFeedbackKind.Counter ? 9 :
            kind == CombatFeedbackKind.HeavyHit ? 8 : kind == CombatFeedbackKind.PlayerHit ? 7 : 5;
        if (entry.clip != null && (lastAudioTick != CombatTimeController.TickCount || priority > lastAudioPriority))
        {
            sound.Stop(); sound.clip = entry.clip; sound.pitch = entry.pitch;
            sound.volume = entry.volume * Mathf.Clamp01(Settings.masterVolume) * Mathf.Clamp01(Settings.sfxVolume);
            sound.Play(); lastAudioTick = CombatTimeController.TickCount; lastAudioPriority = priority;
        }
    }
    public bool FocusCamera(UnityEngine.Object owner, Transform subject, Vector2 offset, float size, float blendSeconds)
    {
        if (!IsReady || owner == null || subject == null || feedbackCamera == null || !feedbackCamera.orthographic ||
            !float.IsFinite(size) || size <= 0 || !float.IsFinite(blendSeconds) || blendSeconds <= 0 ||
            (hasCameraFocus && cameraFocusOwner != owner)) return false;
        if (!hasCameraFocus)
        {
            baseCameraPosition = feedbackCamera.transform.position - appliedShake;
            baseCameraSize = feedbackCamera.orthographicSize;
            cameraFocusBlend = 0; hasCameraFocus = true;
        }
        cameraFocusOwner = owner; cameraSubject = subject; cameraFocusOffset = offset;
        cameraFocusSize = size; cameraBlendSeconds = blendSeconds; cameraFocusRequested = true;
        return true;
    }
    public void ReleaseCameraFocus(UnityEngine.Object owner, bool immediate)
    {
        if (!hasCameraFocus || cameraFocusOwner != owner) return;
        cameraFocusRequested = false;
        if (immediate) RestoreCameraFocus();
    }
    private void RestoreCameraFocus()
    {
        if (!hasCameraFocus) return;
        if (feedbackCamera != null)
        {
            feedbackCamera.transform.position = baseCameraPosition + appliedShake;
            feedbackCamera.orthographicSize = baseCameraSize;
        }
        hasCameraFocus = cameraFocusRequested = false; cameraFocusBlend = 0;
        cameraFocusOwner = null; cameraSubject = null;
    }
    private Effect CreateEffect(int index)
    {
        var root = new GameObject("PooledCombatFeedback_" + index); root.transform.SetParent(transform, false);
        var effect = new Effect { root = root };
        effect.core = CreateLine(root, "Information", 32, .045f);
        effect.accent = CreateLine(root, "Decoration", 31, .065f);
        root.SetActive(false); return effect;
    }
    private LineRenderer CreateLine(GameObject root, string name, int order, float width)
    {
        var child = new GameObject(name); child.transform.SetParent(root.transform, false);
        var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true; line.widthMultiplier = width; line.sortingOrder = order;
        line.numCornerVertices = 0; line.numCapVertices = 0; return line;
    }
    private void DrawEffect(Effect effect)
    {
        float life = Mathf.Clamp01(1 - effect.age / effect.duration);
        float size = effect.size * (1 + (1 - life) * .45f);
        int points = effect.kind == CombatFeedbackKind.LightHit ? 2 : effect.kind == CombatFeedbackKind.JustParry ? 9 :
            effect.kind == CombatFeedbackKind.Counter ? 7 : effect.kind == CombatFeedbackKind.PlayerHit ? 3 :
            effect.kind == CombatFeedbackKind.Parry ? 4 : effect.kind == CombatFeedbackKind.ParryMiss ? 3 : 5;
        effect.core.positionCount = points; effect.core.loop = points > 3;
        for (int i = 0; i < points; i++)
        {
            float angle = i * Mathf.PI * 2 / points + Mathf.PI * .25f;
            Vector2 delta = points == 2 ? new Vector2(i == 0 ? -size : size, i == 0 ? -size * .6f : size * .6f) :
                effect.kind == CombatFeedbackKind.PlayerHit ? new Vector2((i - 1) * size, i == 1 ? -size : size) :
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * (effect.kind == CombatFeedbackKind.JustParry && i % 2 == 1 ? .45f : 1);
            effect.core.SetPosition(i, (Vector3)(effect.position + delta) + Vector3.back * .1f);
        }
        var color = effect.tint; color.a = life * .85f;
        effect.core.startColor = effect.core.endColor = color;
        effect.accent.enabled = effect.decoration > 0;
        effect.accent.positionCount = 2;
        effect.accent.SetPosition(0, (Vector3)(effect.position + Vector2.left * size * 1.5f));
        effect.accent.SetPosition(1, (Vector3)(effect.position + Vector2.right * size * 1.5f));
        color.a = life * .35f * effect.decoration;
        effect.accent.startColor = effect.accent.endColor = color;
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if (CombatTimeController.IsPaused || realSeconds < 0) return;
        foreach (var e in pool)
        {
            if (!e.root.activeSelf) continue;
            e.age += realSeconds;
            if (e.age >= e.duration) e.root.SetActive(false); else DrawEffect(e);
        }
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var f = flashes[i]; f.remaining -= realSeconds;
            if (f.renderer == null || f.remaining <= 0 || Settings.flash <= 0)
            { if (f.renderer != null) f.renderer.color = f.original; flashes.RemoveAt(i); }
        }
        shakeAge += realSeconds;
        if (feedbackCamera != null)
        {
            // Compose focus and shake here so no second component writes the camera.
            if (hasCameraFocus && (cameraFocusOwner == null || cameraSubject == null)) RestoreCameraFocus();
            Vector3 cameraPosition;
            if (hasCameraFocus)
            {
                float focusStrength = Mathf.Clamp01(Settings.shake);
                cameraFocusBlend = focusStrength <= 0 ? 0 : Mathf.MoveTowards(cameraFocusBlend,
                    cameraFocusRequested ? focusStrength : 0, realSeconds / cameraBlendSeconds);
                if (cameraFocusBlend < .00001f) cameraFocusBlend = 0;
                Vector3 subject = cameraSubject.position + (Vector3)cameraFocusOffset;
                subject.z = baseCameraPosition.z;
                cameraPosition = Vector3.Lerp(baseCameraPosition, subject, cameraFocusBlend);
                feedbackCamera.orthographicSize = Mathf.Lerp(baseCameraSize, cameraFocusSize, cameraFocusBlend);
                if (!cameraFocusRequested && cameraFocusBlend == 0) RestoreCameraFocus();
            }
            else
            {
                // Preserve a normal follow-camera writer when no close-up is active.
                baseCameraPosition = feedbackCamera.transform.position - appliedShake;
                cameraPosition = baseCameraPosition;
            }
            float strength = shakeDuration > 0 ? Mathf.Clamp01(1 - shakeAge / shakeDuration) : 0;
            appliedShake = Settings.shake <= 0 ? Vector3.zero : new Vector3(Mathf.Sin(shakeAge * 95), Mathf.Cos(shakeAge * 71) * .4f) * (shakeMagnitude * strength);
            appliedShake.x = Mathf.Round(appliedShake.x * 32) / 32;
            appliedShake.y = Mathf.Round(appliedShake.y * 32) / 32;
            feedbackCamera.transform.position = cameraPosition + appliedShake;
        }
        if (shakeAge >= shakeDuration) shakeMagnitude = shakeDuration = 0;
    }
    private void LateUpdate()
    {
        if (!IsReady) return;
        foreach (var pair in audioVolumes)
            if (pair.Key != null && pair.Key != sound) pair.Key.volume = pair.Value * Mathf.Clamp01(Settings.masterVolume) * Mathf.Clamp01(Settings.sfxVolume);
        if (sound != null && Settings.masterVolume * Settings.sfxVolume <= 0) sound.Stop();
        AdvanceVisuals(Time.unscaledDeltaTime);
    }
    public void ResetPresentation()
    {
        foreach (var e in pool) if (e.root != null) e.root.SetActive(false);
        foreach (var f in flashes) if (f.renderer != null) f.renderer.color = f.original;
        flashes.Clear();
        RestoreCameraFocus();
        if (feedbackCamera != null) feedbackCamera.transform.position -= appliedShake;
        appliedShake = Vector3.zero; shakeAge = shakeDuration = shakeMagnitude = 0;
        sound?.Stop(); lastAudioTick = -1; lastAudioPriority = -1;
        CombatTimeController.ReleaseOwner(this);
    }
}

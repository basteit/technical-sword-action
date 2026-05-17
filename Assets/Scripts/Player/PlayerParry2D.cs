using UnityEngine;
using UnityEngine.InputSystem;

public enum ParryResult
{
    None,
    Normal,
    Just
}

public class PlayerParry2D : MonoBehaviour
{
    [Header("Parry Window")]
    [SerializeField] private float parryWindowDuration = 0.16f;
    [SerializeField] private float justParryDuration = 0.05f;
    [SerializeField] private float parryCooldown = 0.14f;

    [Header("Parry Fail")]
    [SerializeField] private float failLockDuration = 0.24f;

    [Header("References")]
    [SerializeField] private PlayerSpecialSkill2D specialSkill;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalParryClip;
    [SerializeField] private AudioClip justParryClip;
    [SerializeField] private AudioClip parryMissClip;

    private bool parryPressed;
    private bool parryActive;
    private bool parryResolved;
    private float parryTimer;
    private float parryElapsed;
    private float cooldownTimer;
    private float failLockTimer;

    public bool IsParryActive => parryActive;
    public bool IsFailLocked => failLockTimer > 0f;
    public float ParryRemaining => Mathf.Max(0f, parryTimer);
    public float ParryCooldownRemaining => Mathf.Max(0f, cooldownTimer);
    public float FailLockRemaining => Mathf.Max(0f, failLockTimer);
    public ParryResult LastParryResult { get; private set; } = ParryResult.None;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (specialSkill == null)
        {
            specialSkill = GetComponent<PlayerSpecialSkill2D>();
        }
    }

    private void Update()
    {
        ReadInput();
        UpdateTimers();

        if (specialSkill != null && specialSkill.IsUsingSkill)
        {
            parryPressed = false;
            return;
        }

        if (parryPressed && cooldownTimer <= 0f && !parryActive && !IsFailLocked)
        {
            StartParry();
        }

        parryPressed = false;
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            parryPressed = true;
        }
    }

    private void UpdateTimers()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (failLockTimer > 0f)
        {
            failLockTimer -= Time.deltaTime;
        }

        if (!parryActive)
        {
            return;
        }

        parryTimer -= Time.deltaTime;
        parryElapsed += Time.deltaTime;

        if (parryTimer <= 0f)
        {
            parryActive = false;
            LastParryResult = ParryResult.None;

            if (!parryResolved)
            {
                ApplyFailLock();
                PlayClip(parryMissClip, 0.8f);
            }
        }
    }

    private void StartParry()
    {
        parryActive = true;
        parryResolved = false;
        parryTimer = parryWindowDuration;
        parryElapsed = 0f;
        cooldownTimer = parryCooldown;
        LastParryResult = ParryResult.None;
    }

    public bool TryResolveParry(out ParryResult result)
    {
        if (!parryActive)
        {
            result = ParryResult.None;
            return false;
        }

        result = parryElapsed <= justParryDuration ? ParryResult.Just : ParryResult.Normal;
        LastParryResult = result;
        parryActive = false;
        parryResolved = true;
        parryTimer = 0f;

        if (result == ParryResult.Just)
        {
            PlayClip(justParryClip, 1f);
        }
        else
        {
            PlayClip(normalParryClip, 0.95f);
        }

        return true;
    }

    private void ApplyFailLock()
    {
        failLockTimer = failLockDuration;
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }
}


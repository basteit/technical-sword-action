using UnityEngine;

public class CombatCameraFeedback2D : MonoBehaviour
{
    public static CombatCameraFeedback2D Instance { get; private set; }

    [Header("Target")]
    [SerializeField] private Transform cameraRoot;

    [Header("Shake")]
    [SerializeField] private float defaultDuration = 0.08f;
    [SerializeField] private float defaultMagnitude = 0.08f;
    [SerializeField] private float parryDuration = 0.12f;
    [SerializeField] private float parryMagnitude = 0.14f;
    [SerializeField] private float justParryDuration = 0.16f;
    [SerializeField] private float justParryMagnitude = 0.2f;
    [SerializeField] private float damping = 20f;

    private Vector3 originalLocalPos;
    private float shakeTimer;
    private float shakeMagnitude;

    private void Awake()
    {
        Instance = this;

        if (cameraRoot == null && Camera.main != null)
        {
            cameraRoot = Camera.main.transform;
        }

        if (cameraRoot != null)
        {
            originalLocalPos = cameraRoot.localPosition;
        }
    }

    private void LateUpdate()
    {
        if (cameraRoot == null)
        {
            return;
        }

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            Vector2 random = Random.insideUnitCircle * shakeMagnitude;
            cameraRoot.localPosition = originalLocalPos + new Vector3(random.x, random.y, 0f);
            shakeMagnitude = Mathf.Lerp(shakeMagnitude, 0f, damping * Time.deltaTime);
            return;
        }

        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, originalLocalPos, damping * Time.deltaTime);
    }

    public static void PlayHitShake()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.TriggerShake(Instance.defaultDuration, Instance.defaultMagnitude);
    }

    public static void PlayParryShake(ParryResult result)
    {
        if (Instance == null)
        {
            return;
        }

        if (result == ParryResult.Just)
        {
            Instance.TriggerShake(Instance.justParryDuration, Instance.justParryMagnitude);
        }
        else
        {
            Instance.TriggerShake(Instance.parryDuration, Instance.parryMagnitude);
        }
    }

    private void TriggerShake(float duration, float magnitude)
    {
        shakeTimer = Mathf.Max(shakeTimer, duration);
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
    }
}

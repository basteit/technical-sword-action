using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMotor2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private bool flipSpriteByMoveInput = true;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private LayerMask ignoreCollisionLayersWhileDashing;

    [Header("Optional References")]
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;

    [Header("Feel Tuning")]
    [SerializeField] private bool applyRecommendedPhysicsSettings = true;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private float moveInput;
    private bool jumpPressed;
    private bool dashPressed;

    private bool isGrounded;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float originalGravityScale;
    private int facingSign = 1;
    private readonly HashSet<Collider2D> ignoredDashColliders = new();

    public float MoveInput => moveInput;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool CanDash => dashCooldownTimer <= 0f;
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public float DashCooldownRemaining => Mathf.Max(0f, dashCooldownTimer);
    public int FacingSign => facingSign;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
        originalGravityScale = rb.gravityScale;
        facingSign = transform.localScale.x >= 0f ? 1 : -1;

        if (applyRecommendedPhysicsSettings)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (damageReceiver == null)
        {
            damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        }

        if (parry == null)
        {
            parry = GetComponent<PlayerParry2D>();
        }

        if (specialSkill == null)
        {
            specialSkill = GetComponent<PlayerSpecialSkill2D>();
        }
    }

    private void Update()
    {
        ReadInput();
        UpdateGrounded();
        UpdateDashTimers();
        UpdateFacing();

        if (isDashing)
        {
            jumpPressed = false;
            dashPressed = false;
            return;
        }

        if ((damageReceiver != null && damageReceiver.IsHitLocked) ||
            (parry != null && parry.IsFailLocked) ||
            (specialSkill != null && specialSkill.IsUsingSkill))
        {
            jumpPressed = false;
            dashPressed = false;
            return;
        }

        if (jumpPressed && isGrounded)
        {
            Jump();
        }

        if (dashPressed && CanDash)
        {
            StartDash();
        }

        jumpPressed = false;
        dashPressed = false;
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

        if ((damageReceiver != null && damageReceiver.IsHitLocked) ||
            (parry != null && parry.IsFailLocked) ||
            (specialSkill != null && specialSkill.IsUsingSkill))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void ReadInput()
    {
        moveInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                moveInput -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                moveInput += 1f;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpPressed = true;
            }

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)
            {
                dashPressed = true;
            }
        }

        moveInput = Mathf.Clamp(moveInput, -1f, 1f);
    }

    private void UpdateFacing()
    {
        if (!flipSpriteByMoveInput || isDashing)
        {
            return;
        }

        if (moveInput > 0.01f)
        {
            SetFacing(1);
        }
        else if (moveInput < -0.01f)
        {
            SetFacing(-1);
        }
    }

    private void SetFacing(int sign)
    {
        if (sign == 0 || facingSign == sign)
        {
            return;
        }

        facingSign = sign;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingSign;
        transform.localScale = scale;
    }

    private void UpdateGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private void StartDash()
    {
        int dashSign = facingSign;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            dashSign = moveInput > 0f ? 1 : -1;
            SetFacing(dashSign);
        }

        dashDirection = new Vector2(dashSign, 0f);
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDirection * dashSpeed;
        IgnoreDashOverlaps();
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        RestoreDashIgnoredCollisions();
    }

    private void UpdateDashTimers()
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (!isDashing)
        {
            return;
        }

        dashTimer -= Time.deltaTime;

        if (dashTimer <= 0f)
        {
            EndDash();
        }
    }

    private void IgnoreDashOverlaps()
    {
        if (ownCollider == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D other = hits[i];
            if (other == null || other == ownCollider)
            {
                continue;
            }

            if ((ignoreCollisionLayersWhileDashing.value & (1 << other.gameObject.layer)) == 0)
            {
                continue;
            }

            Physics2D.IgnoreCollision(ownCollider, other, true);
            ignoredDashColliders.Add(other);
        }
    }

    private void RestoreDashIgnoredCollisions()
    {
        if (ownCollider == null)
        {
            ignoredDashColliders.Clear();
            return;
        }

        foreach (Collider2D c in ignoredDashColliders)
        {
            if (c != null)
            {
                Physics2D.IgnoreCollision(ownCollider, c, false);
            }
        }

        ignoredDashColliders.Clear();
    }

    private void OnDisable()
    {
        RestoreDashIgnoredCollisions();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}

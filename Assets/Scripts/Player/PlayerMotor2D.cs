using System.Collections.Generic;
using UnityEngine;
using TechnicalSwordAction.PlayerState;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMotor2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private bool flipSpriteByMoveInput = true;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Range(0f, 1f)] private float jumpReleaseMultiplier = 0.5f;
    [SerializeField, Min(1)] private int floorDropFrames = 18;
    private bool jumpHeld;
    private bool variableJumpActive;
    private bool airDashUsed;
    private int groundSuppressFrames;
    private int floorDropRemaining;
    private readonly List<Collider2D> droppedFloors = new();
    public bool AirDashUsed => airDashUsed;
    public bool IsDroppingThrough => droppedFloors.Count > 0;
    public void SetJumpHeld(bool held) => jumpHeld = held;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private LayerMask ignoreCollisionLayersWhileDashing;

    [Header("Optional References")]
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerStateMachine stateMachine;

    [Header("Feel Tuning")]
    [SerializeField] private bool applyRecommendedPhysicsSettings = true;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private float moveInput;
    private bool jumpDownInput;

    private bool isGrounded;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float originalGravityScale;
    private int facingSign = 1;
    private readonly HashSet<Collider2D> ignoredDashColliders = new();

    public float MoveInput => moveInput;
    public bool DownInput { get; private set; }
    // The floor traversal executor in #49 consumes this sample with the Jump request.
    public bool JumpDownInput => jumpDownInput;
    public void SetMoveInput(Vector2 value)
    {
        moveInput = Mathf.Abs(value.x) >= 0.5f ? Mathf.Sign(value.x) : 0f;
        DownInput = value.y <= -0.5f;
    }
    public void SetJumpDownInput(bool down) => jumpDownInput = down;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool CanDash => CanStartDash;
    public bool CanStartDash => isActiveAndEnabled && !isDashing && dashCooldownTimer <= 0f && (isGrounded || !airDashUsed);
    public bool CanStartJump => isActiveAndEnabled && isGrounded && !isDashing;
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public float DashRemaining => Mathf.Max(0f, dashTimer);
    public float DashCooldownRemaining => Mathf.Max(0f, dashCooldownTimer);
    public int FacingSign => facingSign;
    public int CombatTickOrder => 0;

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

        if (attack == null)
        {
            attack = GetComponent<PlayerAttack2D>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }
    }

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    public void CombatTick()
    {
        UpdateGrounded();
        if (variableJumpActive && !isDashing && !jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpReleaseMultiplier);
            variableJumpActive = false;
        }
        if (rb.linearVelocity.y <= 0f) variableJumpActive = false;
        UpdateFacing();
        if (stateMachine == null)
        {
            ApplyCombatVelocity();
        }
    }

    public void CombatTickTimers()
    {
        if (groundSuppressFrames > 0) groundSuppressFrames--;
        if (floorDropRemaining > 0 && --floorDropRemaining == 0)
        {
            bool overlapping = false;
            foreach (Collider2D floor in droppedFloors)
                if (floor != null && ownCollider.bounds.Intersects(floor.bounds)) overlapping = true;
            if (overlapping) floorDropRemaining = 1;
            else RestoreDroppedFloors();
        }
        UpdateDashTimers();
    }

    // The state machine calls this after arbitration, before the clock simulates physics.
    public void ApplyCombatVelocity()
    {
        if (stateMachine != null && stateMachine.LifeState == PlayerLifeState.Dead)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }
        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

        if (stateMachine != null && stateMachine.ActionState == PlayerActionState.Hit)
        {
            // Hit reaction owns the whole velocity until its lock ends.
            return;
        }

        if (stateMachine != null && stateMachine.ActionState == PlayerActionState.Attack && attack != null)
        {
            if (isGrounded)
            {
                // Ground attacks ignore live movement input. The current attack step
                // owns its signed forward/backward motion for the configured window.
                rb.linearVelocity = new Vector2(attack.GroundMotionVelocity, rb.linearVelocity.y);
            }
            else
            {
                // Air attacks retain steering, but at a deliberately reduced speed.
                rb.linearVelocity = new Vector2(moveInput * moveSpeed * attack.AirMoveSpeedMultiplier, rb.linearVelocity.y);
            }

            return;
        }

        bool movementLocked = stateMachine != null
            ? stateMachine.BlocksStandardMovement
            : (damageReceiver != null && damageReceiver.IsHitLocked) ||
              (parry != null && parry.IsFailLocked) ||
              (specialSkill != null && specialSkill.IsUsingSkill);

        if (movementLocked)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    public void ClearSampledInput()
    {
        moveInput = 0f;
        DownInput = false;
        jumpDownInput = false;
        jumpHeld = false;
    }

    private void UpdateFacing()
    {
        if (!flipSpriteByMoveInput || isDashing ||
            (stateMachine != null && stateMachine.ActionState == PlayerActionState.Attack))
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

        isGrounded = false;
        if (groundSuppressFrames > 0 || rb.linearVelocity.y > 0.05f) return;
        foreach (Collider2D floor in Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer))
        {
            if (floor == ownCollider || floor.isTrigger || droppedFloors.Contains(floor) ||
                Physics2D.GetIgnoreCollision(ownCollider, floor)) continue;
            isGrounded = true;
            airDashUsed = false;
            break;
        }
    }

    public bool TryStartJumpFromStateMachine()
    {
        if (!CanStartJump)
        {
            return false;
        }

        bool down = jumpDownInput;
        jumpDownInput = false;
        if (down && TryDropThroughFloor()) return true;
        isGrounded = false;
        groundSuppressFrames = 2;
        variableJumpActive = true;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        return true;
    }

    public bool TryStartDashFromStateMachine()
    {
        if (!CanStartDash)
        {
            return false;
        }

        int dashSign = facingSign;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            dashSign = moveInput > 0f ? 1 : -1;
            SetFacing(dashSign);
        }

        dashDirection = new Vector2(dashSign, 0f);
        if (!isGrounded) airDashUsed = true;
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDirection * dashSpeed;
        IgnoreDashOverlaps();
        return true;
    }

    private void EndDash(bool notifyStateMachine)
    {
        isDashing = false;
        dashTimer = 0f;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;
        RestoreDashIgnoredCollisions();

        if (notifyStateMachine)
        {
            stateMachine?.CompleteAction(PlayerActionState.Dash, "DashComplete");
        }
    }

    public void CancelDashFromStateMachine(bool clearPersistentState = false)
    {
        dashTimer = 0f;
        if (clearPersistentState)
        {
            dashCooldownTimer = 0f;
        }

        if (!isDashing)
        {
            rb.gravityScale = originalGravityScale;
            RestoreDashIgnoredCollisions();
            return;
        }

        EndDash(false);
    }

    private void UpdateDashTimers()
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer = CombatTimeController.AdvanceTimer(dashCooldownTimer);
        }

        if (!isDashing)
        {
            return;
        }

        dashTimer = CombatTimeController.AdvanceTimer(dashTimer);

        if (dashTimer <= 0f)
        {
            EndDash(true);
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

            if (stateMachine != null)
            {
                stateMachine.AcquireCollisionIgnore(PlayerActionState.Dash, ownCollider, other);
            }
            else
            {
                Physics2D.IgnoreCollision(ownCollider, other, true);
                ignoredDashColliders.Add(other);
            }
        }
    }

    private void RestoreDashIgnoredCollisions()
    {
        if (stateMachine != null)
        {
            stateMachine.ReleaseCollisionIgnores(PlayerActionState.Dash);
            ignoredDashColliders.Clear();
            return;
        }

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
        CombatTimeController.Unregister(this);
        bool wasDashing = isDashing;
        CancelDashFromStateMachine(true);
        ResetLocomotion();
        ClearSampledInput();
        if (wasDashing)
        {
            stateMachine?.CompleteAction(PlayerActionState.Dash, "DashDisabled");
        }
    }

    private bool TryDropThroughFloor()
    {
        if (groundCheck == null) return false;
        foreach (Collider2D floor in Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer))
        {
            PlatformEffector2D effector = floor.GetComponent<PlatformEffector2D>();
            if (floor.isTrigger || !floor.usedByEffector || effector == null || !effector.enabled || !effector.useOneWay) continue;
            if (droppedFloors.Contains(floor)) continue;
            Physics2D.IgnoreCollision(ownCollider, floor, true);
            droppedFloors.Add(floor);
        }
        if (droppedFloors.Count == 0) return false;
        floorDropRemaining = Mathf.Max(1, floorDropFrames);
        groundSuppressFrames = floorDropRemaining;
        isGrounded = false;
        variableJumpActive = false;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -2f);
        return true;
    }

    private void RestoreDroppedFloors()
    {
        foreach (Collider2D floor in droppedFloors)
            if (floor != null && ownCollider != null) Physics2D.IgnoreCollision(ownCollider, floor, false);
        droppedFloors.Clear();
        floorDropRemaining = 0;
    }

    public void ResetLocomotion()
    {
        RestoreDroppedFloors();
        groundSuppressFrames = 0;
        airDashUsed = false;
        variableJumpActive = false;
        isGrounded = false;
        ClearSampledInput();
    }

    public void CancelFloorDrop() { RestoreDroppedFloors(); groundSuppressFrames = 0; }

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

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float acceleration = 70f;
    [SerializeField] float deceleration = 80f;
    [SerializeField, Range(0f, 1f)] float airControl = 0.65f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 15f;
    [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.5f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float fallGravityMultiplier = 1.8f;
    [SerializeField] float maxFallSpeed = 20f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 20f;
    [SerializeField] float dashDuration = 0.15f;
    [SerializeField] float dashCooldown = 0.4f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] LayerMask groundLayer;

    Rigidbody2D rb;
    Health health;
    float baseGravity;
    float moveInput;
    bool isGrounded;
    float coyoteCounter;
    float jumpBufferCounter;
    bool isDashing;
    float dashTimer;
    float dashCooldownTimer;

    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public float MoveInput => moveInput;
    public Vector2 Velocity => rb.linearVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        baseGravity = rb.gravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Zero friction: otherwise the player sticks to walls and platform sides in the air
        Collider2D col = GetComponent<Collider2D>();
        if (col && col.sharedMaterial == null)
            col.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
    }

    void Update()
    {
        Vector2 checkPos = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
        isGrounded = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);

        coyoteCounter = isGrounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        jumpBufferCounter -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;

        ReadInput();

        if (jumpBufferCounter > 0f && coyoteCounter > 0f && !isDashing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        if (!isDashing && Mathf.Abs(moveInput) > 0.01f)
            Face(moveInput > 0 ? 1 : -1);
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(FacingDirection * dashSpeed, 0f);
            if (dashTimer <= 0f) EndDash();
            return;
        }

        float target = moveInput * moveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        if (!isGrounded) rate *= airControl;

        float newX = Mathf.MoveTowards(rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);
        float newY = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);
        rb.linearVelocity = new Vector2(newX, newY);

        rb.gravityScale = rb.linearVelocity.y < 0f ? baseGravity * fallGravityMultiplier : baseGravity;
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        float x = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        }
        if (gp != null)
        {
            float stick = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.2f) x = stick;
        }
        moveInput = Mathf.Clamp(x, -1f, 1f);

        bool jumpPressed  = (kb != null && kb.spaceKey.wasPressedThisFrame)  || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        bool jumpReleased = (kb != null && kb.spaceKey.wasReleasedThisFrame) || (gp != null && gp.buttonSouth.wasReleasedThisFrame);
        bool dashPressed  = (kb != null && kb.leftShiftKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;

        if (jumpReleased && rb.linearVelocity.y > 0f && !isDashing)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        if (dashPressed && dashCooldownTimer <= 0f && !isDashing)
            StartDash();
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashDuration + dashCooldown;
        rb.gravityScale = 0f;

        // The player takes no damage while dashing
        if (health) health.MakeInvulnerable(dashDuration);
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = baseGravity;
        rb.linearVelocity = new Vector2(FacingDirection * moveSpeed, 0f);
    }

    void Face(int dir)
    {
        if (dir == FacingDirection) return;
        FacingDirection = dir;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheck ? groundCheck.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(pos, groundCheckSize);
    }
}

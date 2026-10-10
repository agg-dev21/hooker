using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float acceleration = 70f;
    [SerializeField] float deceleration = 80f;
    [SerializeField, Range(0f, 1f)] float airControl = 1f;   // 1 = full control in the air

    [Header("Jump")]
    // Hollow Knight style: while jump is held you rise at a steady speed (Jump Force) for up to Max Jump Hold Time.
    // Let go (or run out of time) and the rise is cut, then gravity takes over. Tap = small hop, hold = about 8.5 tiles.
    [SerializeField] float gravity = 5f;         // player's gravity (higher = snappier jumps and falls)
    [SerializeField] float jumpForce = 23.6f;    // steady rising speed while jump is held
    [SerializeField] float maxJumpHoldTime = 0.12f; // how long you hold Space for the full jump (held jump: about 8.5 tiles)
    [SerializeField] float minJumpHoldTime = 0.05f; // even a quick tap rises at least this long
    [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.4f;    // when the rise ends early, speed is cut to this
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float fallGravityMultiplier = 1.8f;
    [SerializeField] float maxFallSpeed = 26f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 22f;
    [SerializeField] float dashDuration = 0.18f;   // distance = speed x duration (about 4 tiles)
    [SerializeField] float dashCooldown = 0.4f;
    [SerializeField] bool dashThroughEnemies = true; // pass through enemies (cannons, chasers, shooters, bosses) while dashing

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] LayerMask groundLayer;
    [SerializeField] bool standOnAnything = true; // ON: crates, barrels, cannons, enemies and bosses count as ground too

    Rigidbody2D rb;
    Health health;
    float baseGravity;
    float moveInput;
    bool jumpHeld;
    bool isJumping;      // in the steady-rise part of a jump
    float jumpHoldTimer;
    bool isGrounded;
    float coyoteCounter;
    float jumpBufferCounter;
    bool isDashing;
    readonly List<Collider2D> passedThrough = new List<Collider2D>(); // enemies we are currently allowed to pass through
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
        rb.gravityScale = gravity;
        baseGravity = gravity;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep; // keep physics awake so moving hazards still hit an idle player

        // Zero friction: otherwise the player sticks to walls and platform sides in mid-air
        Collider2D col = GetComponent<Collider2D>();
        if (col && col.sharedMaterial == null)
            col.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // paused: ignore input (menu buttons must not jump / dash)

        Vector2 checkPos = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
        isGrounded = standOnAnything ? TouchingSolid(checkPos) : Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);

        coyoteCounter = isGrounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        jumpBufferCounter -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;

        ReadInput();

        if (jumpBufferCounter > 0f && coyoteCounter > 0f && !isDashing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isJumping = true;
            jumpHoldTimer = 0f;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        if (!isDashing && Mathf.Abs(moveInput) > 0.01f)
            Face(moveInput > 0 ? 1 : -1);
    }

    void FixedUpdate()
    {
        if (!isDashing) RestorePassedThrough();

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

        // Steady rise while jump is held
        if (isJumping)
        {
            jumpHoldTimer += Time.fixedDeltaTime;
            bool hitCeiling = rb.linearVelocity.y < jumpForce * 0.5f && jumpHoldTimer > Time.fixedDeltaTime * 2f;
            bool keepRising = (jumpHeld || jumpHoldTimer < minJumpHoldTime) && jumpHoldTimer < maxJumpHoldTime && !hitCeiling;
            if (keepRising) newY = jumpForce;
            else
            {
                isJumping = false;
                if (!jumpHeld && newY > 0f) newY *= jumpCutMultiplier; // let go early: short hop
            }
        }
        rb.linearVelocity = new Vector2(newX, newY);

        // Falling is heavier than rising, for a snappy arc
        rb.gravityScale = rb.linearVelocity.y < 0f ? baseGravity * fallGravityMultiplier : baseGravity;
    }

    void ReadInput()
    {
        // Keyboard + controller, see GameInput.cs for the full button layout
        moveInput = GameInput.MoveX;

        bool jumpPressed  = GameInput.JumpPressed;
        bool jumpReleased = GameInput.JumpReleased;
        jumpHeld = GameInput.JumpHeld;
        bool dashPressed  = GameInput.DashPressed;

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;

        // Let go after the steady rise has ended (already falling back): cut what is left of the rise
        if (jumpReleased && !isJumping && rb.linearVelocity.y > 0f && !isDashing)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        if (dashPressed && dashCooldownTimer <= 0f && !isDashing)
            StartDash();
    }

    // Switched off (hook pull, boat): forget any jump in progress so it does not continue afterwards
    void OnDisable()
    {
        isJumping = false;
    }

    void StartDash()
    {
        isDashing = true;
        isJumping = false;
        dashTimer = dashDuration;
        dashCooldownTimer = dashDuration + dashCooldown;
        rb.gravityScale = 0f;

        // Player can't take damage during a dash
        if (health) health.MakeInvulnerable(dashDuration);

        if (dashThroughEnemies) IgnoreEnemiesOnDashPath();
    }

    // Enemies = anything with an enemy script (chaser, shooter/cannon, boss, touch damage).
    // Walls, crates, breakable walls and tutorial dummies stay solid.
    static bool IsEnemy(Collider2D c)
    {
        return c.GetComponentInParent<EnemyChase>() || c.GetComponentInParent<EnemyShooter>()
            || c.GetComponentInParent<HardyBoss>() || c.GetComponentInParent<ContactDamage>();
    }

    // Stop colliding with every enemy along the dash
    void IgnoreEnemiesOnDashPath()
    {
        float distance = dashSpeed * dashDuration;
        Vector2 center = (Vector2)transform.position + Vector2.right * FacingDirection * distance * 0.5f;
        Vector2 size = new Vector2(distance + 2f, 3f);
        Collider2D[] mine = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (hit.isTrigger || hit.transform.IsChildOf(transform) || passedThrough.Contains(hit) || !IsEnemy(hit)) continue;
            if (mine.Length > 0 && Physics2D.GetIgnoreCollision(mine[0], hit)) continue; // already passable (like Hardy): leave it alone
            foreach (Collider2D c in mine) Physics2D.IgnoreCollision(c, hit, true);
            passedThrough.Add(hit);
        }
    }

    // After the dash: become solid to those enemies again once we are no longer inside them
    void RestorePassedThrough()
    {
        if (passedThrough.Count == 0) return;
        Collider2D[] mine = GetComponentsInChildren<Collider2D>();
        for (int i = passedThrough.Count - 1; i >= 0; i--)
        {
            Collider2D other = passedThrough[i];
            bool inside = false;
            if (other)
                foreach (Collider2D c in mine)
                    if (c.Distance(other).isOverlapped) { inside = true; break; }
            if (inside) continue;

            if (other) foreach (Collider2D c in mine) Physics2D.IgnoreCollision(c, other, false);
            passedThrough.RemoveAt(i);
        }
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

    // Is there any solid (non-trigger) collider under the feet that is not part of the player?
    bool TouchingSolid(Vector2 checkPos)
    {
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(checkPos, groundCheckSize, 0f))
            if (!hit.isTrigger && !hit.transform.IsChildOf(transform)) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheck ? groundCheck.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(pos, groundCheckSize);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Left click  = punch (can be spammed): toward the mouse, damage + light knockback
// X           = punch: toward the held arrow keys, or straight ahead if none are held
// Right click = hook: flies toward the mouse, damages an enemy and pulls it to the player
// Z + Arrows  = hook: arrow first, Z first, or both together all work (two arrows = diagonal). Z alone = forward
// Controller  = X punch, RB / RT hook, aimed with the left stick / D-pad (8 directions) or right stick (free aim)
public class PlayerCombat : MonoBehaviour
{
    [Header("Punch (Left Click / X)")]
    [SerializeField] float punchDistance = 0.9f;  // how far from the player (in the punch direction) the punch lands
    [SerializeField] float punchRadius = 0.5f;
    [SerializeField] int punchDamage = 1;
    [SerializeField] float punchCooldown = 0.12f;
    [SerializeField] float punchKnockback = 0.3f; // how far the enemy gets pushed back

    [Header("Hook (Right Click)")]
    [SerializeField] LineRenderer hookLine;
    [SerializeField] float hookRange = 12f;
    [SerializeField] float hookRadius = 0.3f;

    [Header("Hook: Medium vs Long Range")]
    [SerializeField] float mediumRange = 8f;        // closer than this = medium range, farther = long range
    [SerializeField] float mediumRangeSpeed = 64f;  // hook speed at medium range (slower)
    [SerializeField] float longRangeSpeed = 85f;    // hook speed at long range (faster)
    [SerializeField] int mediumRangeDamage = 2;
    [SerializeField] int longRangeDamage = 3;       // more damage when hit from far away
    [SerializeField] float returnSpeed = 100f;      // speed of the hook returning
    [SerializeField] float hookCooldown = 0.6f;
    [SerializeField] float pullSpeed = 31.5f;
    [SerializeField] float pullStopDistance = 1.2f; // pulled enemy stops this close to the player
    [Header("Poison Hook (lizard's teeth, unlocked by beating the Level 2 boss)")]
    [SerializeField] int poisonHookDamage = 2;          // poison damage per tick
    [SerializeField] float poisonHookDuration = 4f;
    [SerializeField] float poisonHookTickInterval = 0.5f;
    [SerializeField] Color poisonHookColor = new Color(0.4f, 0.95f, 0.2f);
    [SerializeField] LayerMask groundLayer;         // hook returns when it hits these layers (walls)
    [SerializeField] float ringClearance = 2.5f;    // ring launch: how high the player's feet go above the floor above the ring

    [Header("Hook: Z + Arrow Keys")]
    [SerializeField] float maxZHold = 0.6f;        // holding Z with no arrow: wait this long for an arrow, then fire forward
    [SerializeField] float afterZRelease = 0.2f;   // Z tapped and released: still accept an arrow for this long
    [SerializeField] float diagonalGrace = 0.04f;  // extra time after the first arrow to catch a second one (diagonal)
    [SerializeField] float aimAssistCone = 60f;    // Z/arrow hook: snap to a ring within this angle of the aim (0 = off)

    PlayerController2D controller;
    float punchTimer;
    float hookTimer;
    bool hookOut;
    bool waitingForArrow;     // Z was pressed with no arrow held; waiting for one
    float zHoldTime;          // how long we've been waiting while Z is held
    float zReleaseTime = -1f; // time since Z was released (-1 = still held)
    float arrowHeldTime = -1f;
    Vector2 lastArrowAim;     // remembered in case the arrow is only tapped
    float riseGravity = 1f;   // player's normal gravity scale while rising (used for the ring launch)

    int Facing => controller ? controller.FacingDirection : 1;
    public float AttackSpeedMultiplier { get; set; } = 1f; // items (speed boost) change this
    public float LastPunchTime { get; private set; } = -99f; // for the player's animation (PlayerArt)
    public float LastHookTime { get; private set; } = -99f;
    public Vector2 LastAttackDir { get; private set; } = Vector2.right;
    public bool HookOut => hookOut;

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        if (hookLine) hookLine.enabled = false;
        if (TryGetComponent(out Rigidbody2D body)) riseGravity = body.gravityScale;

        // Ground Layer left as "Nothing" in the Inspector: use the "Ground" layer automatically.
        // Without it the hook goes through walls and the ring climb can't find the floor above.
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
            if (groundLayer.value == 0)
                Debug.LogWarning("PlayerCombat: no 'Ground' layer found. Set Ground Layer in the Inspector.", this);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // game paused
        if (Companion.IsAiming) return;   // aiming Ariel's light: clicks are for her

        punchTimer -= Time.deltaTime * AttackSpeedMultiplier; // speed boost: attacks recharge faster
        hookTimer -= Time.deltaTime * AttackSpeedMultiplier;

        bool canHook = hookTimer <= 0f && !hookOut && !(controller && controller.IsRooted); // no hooking while chained

        // Controller hook (RB / RT): fires right away toward the stick (or straight ahead)
        if (canHook && GameInput.HookPadPressed)
            FireHook(GetArrowAim(), true);

        // Z + arrows: arrow first / Z first / both together all work
        HandleKeyboardHook(canHook);

        // X key or controller X = punch toward the aim (arrows / stick), or straight ahead
        if ((GameInput.PunchKeyPressed || GameInput.PunchPadPressed) && punchTimer <= 0f)
            Punch(GetArrowAim());

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && punchTimer <= 0f)
            Punch(GetMouseDirection());

        if (mouse.rightButton.wasPressedThisFrame && canHook && !hookOut)
            FireHook(GetMouseDirection());
    }

    void HandleKeyboardHook(bool canHook)
    {
        // Z pressed
        if (canHook && !waitingForArrow && GameInput.HookKeyPressed)
        {
            // Arrow already held (or pressed this same frame): fire immediately
            if (ArrowsHeld()) { FireHook(GetArrowAim(), true); return; }

            // No arrow yet: wait for one
            waitingForArrow = true;
            zHoldTime = 0f;
            zReleaseTime = GameInput.HookKeyHeld ? -1f : 0f;
            arrowHeldTime = -1f;
            return;
        }

        if (!waitingForArrow) return;

        // An arrow arrived: wait a moment longer so a second arrow (diagonal) can be caught
        if (ArrowsHeld())
        {
            lastArrowAim = GetArrowAim();
            arrowHeldTime = arrowHeldTime < 0f ? 0f : arrowHeldTime + Time.deltaTime;
            if (arrowHeldTime >= diagonalGrace) FireHook(lastArrowAim, true);
            return;
        }

        // Arrow was only tapped (released before the diagonal grace ended): use the remembered aim
        if (arrowHeldTime >= 0f) { FireHook(lastArrowAim, true); return; }

        if (GameInput.HookKeyHeld)
        {
            // Z still held: keep waiting for an arrow (up to maxZHold)
            zHoldTime += Time.deltaTime;
            if (zHoldTime >= maxZHold) FireHook(new Vector2(Facing, 0f), true);
        }
        else
        {
            // Z released: allow a short extra window for the arrow, then fire forward
            zReleaseTime = zReleaseTime < 0f ? 0f : zReleaseTime + Time.deltaTime;
            if (zReleaseTime >= afterZRelease) FireHook(new Vector2(Facing, 0f), true);
        }
    }

    void FireHook(Vector2 dir, bool aimAssist = false)
    {
        waitingForArrow = false;
        arrowHeldTime = -1f;
        if (hookOut) return;
        if (aimAssist) dir = AssistAim(dir);
        StartCoroutine(Hook(dir));
    }

    // Arrows only give 8 directions, so a ring is rarely exactly on that line.
    // So: if a visible ring is within aimAssistCone of the aim, send the hook straight at it.
    // Rings sit high up in the ceiling hole, so the angle grows fast when standing to the side - hence the wide cone (60).
    Vector2 AssistAim(Vector2 dir)
    {
        if (aimAssistCone <= 0f) return dir;

        Vector2 origin = transform.position;
        HookPoint best = null;
        float bestAngle = aimAssistCone;

        foreach (HookPoint hp in HookPoint.All)
        {
            if (!hp) continue;
            Vector2 ringPos = hp.transform.position;
            Vector2 toRing = ringPos - origin;
            if (toRing.sqrMagnitude < 0.01f) continue;

            // The hook must be able to reach it: on screen (or within range if there is no camera)
            if (Camera.main ? !InsideScreen(ringPos) : toRing.magnitude > hookRange) continue;

            float angle = Vector2.Angle(dir, toRing);
            if (angle > bestAngle) continue;           // too far from the aim
            if (WallBetween(origin, ringPos)) continue; // wall in the way

            bestAngle = angle;
            best = hp;
        }

        return best ? ((Vector2)best.transform.position - origin).normalized : dir;
    }

    bool WallBetween(Vector2 a, Vector2 b)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(a, b, groundLayer))
            if (!hit.collider.isTrigger && !hit.transform.IsChildOf(transform)) return true;
        return false;
    }

    // Aiming with arrows, D-pad or a stick right now?
    bool ArrowsHeld() => GameInput.AimHeld;

    // Aim from the arrows / D-pad / left stick (8 directions) or right stick (free aim)
    Vector2 GetArrowAim()
    {
        if (GameInput.TryGetAim(out Vector2 dir)) return dir;
        return new Vector2(Facing, 0f); // not aiming: straight ahead
    }

    // ---------------- PUNCH ----------------
    void Punch(Vector2 dir) // dir comes from the mouse or the arrow keys
    {
        punchTimer = punchCooldown;
        LastPunchTime = Time.time;
        LastAttackDir = dir;
        HashSet<Health> alreadyHit = new HashSet<Health>(); // hit each enemy only once per punch

        Vector2 punchCenter = (Vector2)transform.position + dir * punchDistance;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(punchCenter, punchRadius))
        {
            if (hit.transform.IsChildOf(transform)) continue; // don't hit yourself
            if (!hit.TryGetComponent(out Health health) || alreadyHit.Contains(health)) continue;
            alreadyHit.Add(health);

            health.TakeDamage(Mathf.RoundToInt(punchDamage * PlayerBalance.Current.punchDamageMultiplier));

            // Light knockback: nudge the enemy in the punch direction
            Vector2 push = dir * punchKnockback;
            Rigidbody2D rb = hit.attachedRigidbody;
            if (rb) rb.position += push;
            else hit.transform.position += (Vector3)push;
        }
    }

    // ---------------- HOOK ----------------
    IEnumerator Hook(Vector2 dir) // dir comes from the mouse or the arrow keys
    {
        hookOut = true;
        LastHookTime = Time.time;
        LastAttackDir = dir;
        hookTimer = hookCooldown;
        Vector2 tip = transform.position;
        float traveled = 0f;
        Health target = null;
        HookPoint anchor = null;

        if (hookLine) hookLine.enabled = true;

        // 1) Hook flies out
        // It travels as far as it likes on screen and returns once it leaves the camera view
        while (Camera.main ? InsideScreen(tip) : traveled < hookRange)
        {
            float step = (traveled < mediumRange ? mediumRangeSpeed : longRangeSpeed) * Time.deltaTime;
            Vector2 prevTip = tip;
            tip += dir * step;
            traveled += step;
            DrawLine(tip);

            // Check the whole path from the last position to the new one (so a fast hook can't skip over a ring)
            bool hitWall = false;
            foreach (RaycastHit2D r in Physics2D.CircleCastAll(prevTip, hookRadius, dir, step))
            {
                Collider2D h = r.collider;
                if (h.transform.IsChildOf(transform)) continue;
                if (h.TryGetComponent(out HookPoint hpnt)) { anchor = hpnt; break; }
                if (h.TryGetComponent(out Health hp)) { target = hp; break; }
                if (!h.isTrigger && (groundLayer.value & (1 << h.gameObject.layer)) != 0) { hitWall = true; break; } // wall came first: stop
            }
            if (anchor || target || hitWall) break;

            yield return null;
        }

        // Hit a ring: pull the player into the ring, then launch them up through the hole
        if (anchor)
        {
            Rigidbody2D myRb = GetComponent<Rigidbody2D>();
            Collider2D myCol = GetComponent<Collider2D>();
            if (controller) controller.enabled = false; // disable movement while being pulled

            // Kinematic while pulling: the player slides into the hole without snagging on the ceiling edge
            myRb.linearVelocity = Vector2.zero;
            myRb.bodyType = RigidbodyType2D.Kinematic;

            Vector2 point = anchor.transform.position;
            yield return MoveKinematic(myRb, point, 2f);

            // This ring leads to the next scene: hang on the ring, fade to black, load the scene
            if (!string.IsNullOrEmpty(anchor.NextScene))
            {
                myRb.bodyType = RigidbodyType2D.Dynamic;
                myRb.linearVelocity = Vector2.zero;
                if (hookLine) hookLine.enabled = false;
                ScreenFade.FadeToScene(anchor.NextScene);
                yield break;
            }

            if (hookLine) hookLine.enabled = false;

            // Launch straight up, high enough to clear the floor above by ringClearance.
            // The player steers onto the floor themselves (A / D) - no automatic sideways drag.
            float launch = anchor.LaunchSpeed;
            if (myCol && FindFloorAbove(point, out float floorTop))
            {
                float feet = point.y - myCol.bounds.extents.y;
                float rise = floorTop + ringClearance - feet;
                float g = Mathf.Abs(Physics2D.gravity.y) * riseGravity;
                if (rise > 0f) launch = Mathf.Max(launch, Mathf.Sqrt(2f * g * rise));
            }

            myRb.bodyType = RigidbodyType2D.Dynamic;
            myRb.gravityScale = riseGravity;
            myRb.linearVelocity = new Vector2(0f, launch);

            if (controller) controller.enabled = true; // the controller restores gravity itself
        }

        // 2) Hit an enemy: deal damage and pull it in
        if (target)
        {
            target.TakeDamage(Mathf.RoundToInt((traveled <= mediumRange ? mediumRangeDamage : longRangeDamage) * PlayerBalance.Current.hookDamageMultiplier));
            if (PlayerUpgrades.PoisonHook) // the lizard's teeth on the hook
                Poison.Apply(target.gameObject, poisonHookDamage + PlayerBalance.Current.poisonHookTickBonus, poisonHookDuration, poisonHookTickInterval);

            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // Pause the enemy's own movement scripts while pulling, otherwise they cancel the pull
                List<MonoBehaviour> paused = new List<MonoBehaviour>();
                foreach (MonoBehaviour mb in target.GetComponents<MonoBehaviour>())
                {
                    if (!mb.enabled || mb is Health || mb is ContactDamage) continue;
                    mb.enabled = false;
                    paused.Add(mb);
                }

                float t = 0f;
                while (target && rb && t < 1f)
                {
                    Vector2 toPlayer = (Vector2)transform.position - rb.position;
                    if (toPlayer.magnitude <= pullStopDistance) break;

                    rb.linearVelocity = toPlayer.normalized * pullSpeed;
                    tip = rb.position;
                    DrawLine(tip);

                    t += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }

                if (target && rb) rb.linearVelocity = Vector2.zero;

                // Pull finished: enemy back to normal
                foreach (MonoBehaviour mb in paused)
                    if (mb) mb.enabled = true;
            }
        }

        // 3) Hook returns
        while (true)
        {
            Vector2 origin = transform.position;
            tip = Vector2.MoveTowards(tip, origin, returnSpeed * Time.deltaTime);
            DrawLine(tip);
            if (Vector2.Distance(tip, origin) < 0.1f) break;
            yield return null;
        }

        if (hookLine) hookLine.enabled = false;
        hookOut = false;
    }

    // Moves a kinematic body to a point at pullSpeed (gives up after maxTime seconds)
    IEnumerator MoveKinematic(Rigidbody2D body, Vector2 target, float maxTime)
    {
        float t = 0f;
        while (t < maxTime && Vector2.Distance(body.position, target) > 0.05f)
        {
            body.MovePosition(Vector2.MoveTowards(body.position, target, pullSpeed * Time.fixedDeltaTime));
            if (hookLine && hookLine.enabled) DrawLine(target);
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // Finds the top of the floor beside the hole a ring sits in (the floor the player climbs onto)
    bool FindFloorAbove(Vector2 ring, out float floorTop)
    {
        floorTop = float.MinValue;
        if (groundLayer.value == 0) return false;

        foreach (float side in new[] { -1f, 1f })
        {
            // Distance from the ring to the hole edge on this side
            RaycastHit2D edge = Physics2D.Raycast(ring, Vector2.right * side, 5f, groundLayer);
            if (!edge) continue;

            // Look down onto the floor just past the edge
            Vector2 from = new Vector2(ring.x + side * (edge.distance + 0.3f), ring.y + 6f);
            if (Physics2D.OverlapPoint(from, groundLayer)) continue; // inside a wall
            RaycastHit2D floor = Physics2D.Raycast(from, Vector2.down, 8f, groundLayer);
            if (floor && floor.point.y > ring.y) floorTop = Mathf.Max(floorTop, floor.point.y);
        }
        return floorTop > float.MinValue;
    }

    // Direction from the player to the mouse
    Vector2 GetMouseDirection()
    {
        if (Camera.main != null && Mouse.current != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 d = (Vector2)(mouseWorld - transform.position);
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        return new Vector2(Facing, 0f); // mouse is right on the player: straight ahead
    }

    // Whether a point is inside the camera view
    bool InsideScreen(Vector2 point)
    {
        Vector3 v = Camera.main.WorldToViewportPoint(point);
        return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }

    void DrawLine(Vector2 tip)
    {
        if (!hookLine) return;
        if (PlayerUpgrades.PoisonHook) hookLine.startColor = hookLine.endColor = poisonHookColor; // green: poison teeth on the hook
        hookLine.positionCount = 2;
        hookLine.SetPosition(0, transform.position);
        hookLine.SetPosition(1, tip);
    }

    void OnDrawGizmosSelected()
    {
        // Red circle = punch area (drawn straight ahead in the editor; in game it follows the aim)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.right * punchDistance, punchRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * hookRange);
    }
}
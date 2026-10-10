using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Left click  = punch (spammable): toward the mouse, damage + light knockback
// Right click = hook: flies toward the mouse, damages enemies and pulls them toward the player
public class PlayerCombat : MonoBehaviour
{
    [Header("Punch (Left Click)")]
    [SerializeField] float punchDistance = 1.6f;  // how far from the player (toward the mouse) the punch lands
    [SerializeField] float punchRadius = 0.8f;
    [SerializeField] int punchDamage = 1;
    [SerializeField] float punchCooldown = 0.12f;
    [SerializeField] float punchKnockback = 0.3f; // how far enemies get pushed back

    [Header("Hook (Right Click)")]
    [SerializeField] LineRenderer hookLine;
    [SerializeField] float hookRange = 12f;
    [SerializeField] float hookRadius = 0.3f;

    [Header("Hook: Medium vs Long Range")]
    [SerializeField] float mediumRange = 8f;        // closer than this = medium range, farther = long range
    [SerializeField] float mediumRangeSpeed = 64f;  // hook speed within medium range (a bit slower)
    [SerializeField] float longRangeSpeed = 85f;    // hook speed beyond medium range (faster)
    [SerializeField] int mediumRangeDamage = 2;
    [SerializeField] int longRangeDamage = 3;       // hits from far away do more damage
    [SerializeField] float returnSpeed = 100f;      // speed of the hook coming back
    [SerializeField] float hookCooldown = 0.6f;
    [SerializeField] float pullSpeed = 31.5f;
    [SerializeField] float pullStopDistance = 1.2f; // pulled enemies stop this close
    [SerializeField] LayerMask groundLayer;         // the hook bounces back off these layers

    PlayerController2D controller;
    float punchTimer;
    float hookTimer;
    bool hookOut;

    int Facing => controller ? controller.FacingDirection : 1;

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        if (hookLine) hookLine.enabled = false;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // game paused

        punchTimer -= Time.deltaTime;
        hookTimer -= Time.deltaTime;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && punchTimer <= 0f)
            Punch();

        if (mouse.rightButton.wasPressedThisFrame && hookTimer <= 0f && !hookOut)
            StartCoroutine(Hook());
    }

    // ---------------- PUNCH ----------------
    void Punch()
    {
        punchTimer = punchCooldown;
        HashSet<Health> alreadyHit = new HashSet<Health>(); // hit each enemy only once per punch

        // The punch lands toward the mouse
        Vector2 dir = GetMouseDirection();
        Vector2 punchCenter = (Vector2)transform.position + dir * punchDistance;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(punchCenter, punchRadius))
        {
            if (hit.transform.IsChildOf(transform)) continue; // do not hit yourself
            if (!hit.TryGetComponent(out Health health) || alreadyHit.Contains(health)) continue;
            alreadyHit.Add(health);

            health.TakeDamage(punchDamage);

            // Light knockback: nudge the enemy in the punch direction
            Vector2 push = dir * punchKnockback;
            Rigidbody2D rb = hit.attachedRigidbody;
            if (rb) rb.position += push;
            else hit.transform.position += (Vector3)push;
        }
    }

    // ---------------- HOOK ----------------
    IEnumerator Hook()
    {
        hookOut = true;
        hookTimer = hookCooldown;

        Vector2 dir = GetMouseDirection(); // the hook flies toward the mouse
        Vector2 tip = transform.position;
        float traveled = 0f;
        Health target = null;
        HookPoint anchor = null;

        if (hookLine) hookLine.enabled = true;

        // 1) The hook flies out
        // It can go as far as it likes inside the screen; leaving the frame sends it back
        while (Camera.main ? InsideScreen(tip) : traveled < hookRange)
        {
            float step = (traveled < mediumRange ? mediumRangeSpeed : longRangeSpeed) * Time.deltaTime;
            tip += dir * step;
            traveled += step;
            DrawLine(tip);

            bool hitWall = false;
            foreach (Collider2D h in Physics2D.OverlapCircleAll(tip, hookRadius))
            {
                if (h.transform.IsChildOf(transform)) continue;
                if (h.TryGetComponent(out HookPoint hpnt)) { anchor = hpnt; break; }
                if (h.TryGetComponent(out Health hp)) { target = hp; break; }
                if (!h.isTrigger && (groundLayer.value & (1 << h.gameObject.layer)) != 0) hitWall = true;
            }
            if (anchor || target || hitWall) break;

            yield return null;
        }

        // Hit a ring: the player is pulled to the ring, then launched upward
        if (anchor)
        {
            Rigidbody2D myRb = GetComponent<Rigidbody2D>();
            if (controller) controller.enabled = false; // no movement while being pulled
            myRb.gravityScale = 0f;

            Vector2 point = anchor.transform.position;
            float t = 0f;
            while (t < 1.5f)
            {
                Vector2 toRing = point - myRb.position;
                if (toRing.magnitude <= 0.3f) break;

                myRb.linearVelocity = toRing.normalized * pullSpeed;
                tip = point;
                DrawLine(tip);

                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // This ring leads to another scene: hang on the ring, fade to black, load the scene
            if (!string.IsNullOrEmpty(anchor.NextScene))
            {
                myRb.linearVelocity = Vector2.zero;
                if (hookLine) hookLine.enabled = false;
                ScreenFade.FadeToScene(anchor.NextScene);
                yield break;
            }

            myRb.linearVelocity = new Vector2(0f, anchor.LaunchSpeed);
            if (controller) controller.enabled = true; // the controller restores gravity itself
        }

        // 2) Hit an enemy: damage it and pull it
        if (target)
        {
            target.TakeDamage(traveled <= mediumRange ? mediumRangeDamage : longRangeDamage);

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

                // Pull finished: the enemy goes back to normal
                foreach (MonoBehaviour mb in paused)
                    if (mb) mb.enabled = true;
            }
        }

        // 3) The hook comes back
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

    // Direction from the player to the mouse
    Vector2 GetMouseDirection()
    {
        if (Camera.main != null && Mouse.current != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 d = (Vector2)(mouseWorld - transform.position);
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        return new Vector2(Facing, 0f); // if the mouse is right on the player, aim straight ahead
    }

    // Is the point inside the camera frame?
    bool InsideScreen(Vector2 point)
    {
        Vector3 v = Camera.main.WorldToViewportPoint(point);
        return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }

    void DrawLine(Vector2 tip)
    {
        if (!hookLine) return;
        hookLine.positionCount = 2;
        hookLine.SetPosition(0, transform.position);
        hookLine.SetPosition(1, tip);
    }

    void OnDrawGizmosSelected()
    {
        // Red circle = punch area (shown straight ahead in the editor, toward the mouse in game)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.right * punchDistance, punchRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * hookRange);
    }
}

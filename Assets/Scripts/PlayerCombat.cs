using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Left click  = punch (spam kar sakte ho): mouse ki taraf, damage + halka sa knockback
// Right click = hook: mouse ki taraf jaata hai, enemy ko damage deta hai aur player ki taraf kheenchta hai
public class PlayerCombat : MonoBehaviour
{
    [Header("Punch (Left Click)")]
    [SerializeField] float punchDistance = 0.9f;  // player se mouse ki taraf punch kitna door lage
    [SerializeField] float punchRadius = 0.5f;
    [SerializeField] int punchDamage = 1;
    [SerializeField] float punchCooldown = 0.12f;
    [SerializeField] float punchKnockback = 0.3f; // enemy kitna peeche khiske

    [Header("Hook (Right Click)")]
    [SerializeField] LineRenderer hookLine;
    [SerializeField] float hookRange = 12f;
    [SerializeField] float hookSpeed = 68f;
    [SerializeField] float hookRadius = 0.3f;
    [SerializeField] int hookDamage = 2;
    [SerializeField] float hookCooldown = 0.6f;
    [SerializeField] float pullSpeed = 31.5f;
    [SerializeField] float pullStopDistance = 1.2f; // enemy itna paas aake ruk jaye
    [SerializeField] LayerMask groundLayer;         // hook deewar pe lage toh wapas aaye

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
        HashSet<Health> alreadyHit = new HashSet<Health>(); // ek punch mein ek enemy ko ek hi baar

        // Punch mouse ki taraf lagta hai
        Vector2 dir = GetMouseDirection();
        Vector2 punchCenter = (Vector2)transform.position + dir * punchDistance;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(punchCenter, punchRadius))
        {
            if (hit.transform.IsChildOf(transform)) continue; // khud ko mat maaro
            if (!hit.TryGetComponent(out Health health) || alreadyHit.Contains(health)) continue;
            alreadyHit.Add(health);

            health.TakeDamage(punchDamage);

            // Halka sa knockback: enemy ko punch ki direction mein thoda khiska do
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

        Vector2 dir = GetMouseDirection(); // hook mouse ki taraf jaayega
        Vector2 tip = transform.position;
        float traveled = 0f;
        Health target = null;
        HookPoint anchor = null;

        if (hookLine) hookLine.enabled = true;

        // 1) Hook bahar jaata hai
        // Screen ke andar jitna chahe utna door jaata hai, frame se bahar nikla toh wapas
        while (Camera.main ? InsideScreen(tip) : traveled < hookRange)
        {
            float step = hookSpeed * Time.deltaTime;
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

        // Ring ko laga: player khud ring tak khinchta hai, phir upar uchhalta hai
        if (anchor)
        {
            Rigidbody2D myRb = GetComponent<Rigidbody2D>();
            if (controller) controller.enabled = false; // khinchte waqt movement band
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

            // Ye ring agle scene ki taraf le jaata hai: ring pe latko, screen black, naya scene
            if (!string.IsNullOrEmpty(anchor.NextScene))
            {
                myRb.linearVelocity = Vector2.zero;
                if (hookLine) hookLine.enabled = false;
                ScreenFade.FadeToScene(anchor.NextScene);
                yield break;
            }

            myRb.linearVelocity = new Vector2(0f, anchor.LaunchSpeed);
            if (controller) controller.enabled = true; // gravity controller khud wapas set karta hai
        }

        // 2) Enemy ko laga: damage do aur kheencho
        if (target)
        {
            target.TakeDamage(hookDamage);

            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // Kheenchte waqt enemy ki apni chalne wali scripts rok do, warna wo kheenchna cancel kar dengi
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

                // Kheenchna khatam: enemy wapas normal
                foreach (MonoBehaviour mb in paused)
                    if (mb) mb.enabled = true;
            }
        }

        // 3) Hook wapas aata hai
        while (true)
        {
            Vector2 origin = transform.position;
            tip = Vector2.MoveTowards(tip, origin, hookSpeed * 1.5f * Time.deltaTime);
            DrawLine(tip);
            if (Vector2.Distance(tip, origin) < 0.1f) break;
            yield return null;
        }

        if (hookLine) hookLine.enabled = false;
        hookOut = false;
    }

    // Player se mouse ki taraf ki direction
    Vector2 GetMouseDirection()
    {
        if (Camera.main != null && Mouse.current != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 d = (Vector2)(mouseWorld - transform.position);
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        return new Vector2(Facing, 0f); // mouse player ke upar hi ho toh seedha aage
    }

    // Point camera ke frame ke andar hai ya nahi
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
        // Red circle = punch ka area (editor mein seedha aage dikhata hai, game mein mouse ki taraf)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.right * punchDistance, punchRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * hookRange);
    }
}

using UnityEngine;

// Venom-style goo tentacle. Shoots out of a wall/ceiling/floor on a timer.
// First it flashes (warning), then it extends. Touching it deals damage + knockback.
// It is SOLID: the player cannot pass through it (Is Trigger OFF).
// Needs a SpriteRenderer + BoxCollider2D. The object's position = the point it grows out of.
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class GooTendril : MonoBehaviour
{
    [SerializeField] Vector2 direction = Vector2.down; // direction it extends in
    [SerializeField] float length = 8f;
    [SerializeField] float thickness = 0.6f;

    [Header("Timing")]
    [SerializeField] float startDelay = 0f;  // offset so different tendrils fire at different times
    [SerializeField] float restTime = 1.5f;  // time spent hidden
    [SerializeField] float warnTime = 0.6f;  // time spent flashing
    [SerializeField] float warnPeek = 0.4f;  // how far it peeks out during the warning
    [SerializeField] float warnFlashSpeed = 8f;
    [SerializeField] float outTime = 0.8f;   // time spent fully extended
    [SerializeField] float extendSpeed = 40f;

    [Header("Damage")]
    [SerializeField] int damage = 1;
    [SerializeField] float knockback = 10f;
    [SerializeField] float knockbackUp = 0.5f; // upward part of the knockback (fraction of knockback)

    [Header("Colors")]
    [SerializeField] Color gooColor = new Color(0.08f, 0.05f, 0.12f);
    [SerializeField] Color warnColor = new Color(0.6f, 0.1f, 0.9f);

    enum State { Rest, Warn, Out }

    State state = State.Rest;
    Vector3 basePos;
    SpriteRenderer sr;
    BoxCollider2D col;
    float current;
    float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = false; // SOLID: blocks the player
        col.size = Vector2.one;

        // Moving solid collider -> Kinematic Rigidbody2D so physics pushes the player out correctly
        var rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.None;

        direction = direction.normalized;
        basePos = transform.position;
        transform.right = direction; // length points along the direction
        timer = -startDelay;
        SetLength(0f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float target = 0f;

        switch (state)
        {
            case State.Rest:
                if (timer >= restTime) { state = State.Warn; timer = 0f; }
                break;
            case State.Warn:
                target = warnPeek; // peek out a little
                if (timer >= warnTime) { state = State.Out; timer = 0f; }
                break;
            case State.Out:
                target = length;
                if (timer >= outTime) { state = State.Rest; timer = 0f; }
                break;
        }

        current = Mathf.MoveTowards(current, target, extendSpeed * Time.deltaTime);
        SetLength(current);

        col.enabled = state == State.Out && current > 0.5f;
        sr.color = state == State.Warn && Mathf.PingPong(timer * warnFlashSpeed, 1f) > 0.5f ? warnColor : gooColor;
    }

    void SetLength(float l)
    {
        transform.localScale = new Vector3(Mathf.Max(l, 0.001f), thickness, 1f);
        transform.position = basePos + (Vector3)(direction * l * 0.5f);
        sr.enabled = l > 0.01f;
    }

    // Solid collider -> use Collision events instead of Trigger events
    void OnCollisionEnter2D(Collision2D collision) => HitPlayer(collision);
    void OnCollisionStay2D(Collision2D collision)  => HitPlayer(collision);

    void HitPlayer(Collision2D collision)
    {
        GameObject other = collision.gameObject;
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;

        int before = health.CurrentHealth;
        health.TakeDamage(damage);

        // Only knock back if damage actually landed (none during a dash)
        if (health.CurrentHealth < before && collision.rigidbody)
            collision.rigidbody.linearVelocity = KnockbackFor(other.transform.position);
    }

    // Push the player away from the tendril (sideways if touched from the side; sweeping tendrils push along their direction)
    Vector2 KnockbackFor(Vector2 playerPos)
    {
        Vector2 push;
        bool horizontalTendril = Mathf.Abs(direction.y) < 0.5f;

        if (horizontalTendril)
        {
            push = direction * knockback; // floor sweeper: pushes forward
        }
        else
        {
            Vector2 perp = new Vector2(-direction.y, direction.x);
            float side = Vector2.Dot(playerPos - (Vector2)basePos, perp) >= 0f ? 1f : -1f;
            push = perp * side * knockback; // vertical tendril: push toward the side the player is on
        }

        return push + Vector2.up * knockback * knockbackUp;
    }
}
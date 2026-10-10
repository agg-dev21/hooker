using UnityEngine;

// Venom-style goo tentacle. Shoots out of a wall/ceiling/floor on a timer.
// It flashes first (warning), then extends. Touching it deals damage and knockback.
// Solid collider stops the player from passing through. Needs a SpriteRenderer and BoxCollider2D.
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class GooTendril : MonoBehaviour
{
    [SerializeField] Vector2 direction = Vector2.down; // direction it extends
    [SerializeField] float length = 8f;
    [SerializeField] float thickness = 0.6f;

    [Header("Timing")]
    [SerializeField] float startDelay = 0f;  // offset so different tendrils fire at different times
    [SerializeField] float restTime = 1.5f;  // time spent hidden
    [SerializeField] float warnTime = 0.6f;  // time spent flashing (warning)
    [SerializeField] float warnPeek = 0.4f;  // how far it pokes out during the warning
    [SerializeField] float warnFlashSpeed = 8f;
    [SerializeField] float outTime = 0.8f;   // time spent extended
    [SerializeField] float extendSpeed = 40f;

    [Header("Damage")]
    [SerializeField] int damage = 10;
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
        col.isTrigger = false;
        col.size = Vector2.one;

        var rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.None;

        direction = direction.normalized;
        basePos = transform.position;
        transform.right = direction; // length runs along the direction
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
                target = warnPeek; // pokes out a little
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

    void OnCollisionEnter2D(Collision2D collision) => HitPlayer(collision);
    void OnCollisionStay2D(Collision2D collision) => HitPlayer(collision);

    void HitPlayer(Collision2D collision)
    {
        GameObject other = collision.gameObject;
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;

        int before = health.CurrentHealth;
        health.TakeDamage(damage);

        // Knock back only if the damage landed (it does not while dashing)
        if (health.CurrentHealth < before && collision.rigidbody)
            collision.rigidbody.linearVelocity = KnockbackFor(other.transform.position);
    }

    Vector2 KnockbackFor(Vector2 playerPos)
    {
        Vector2 push;
        bool horizontalTendril = Mathf.Abs(direction.y) < 0.5f;

        if (horizontalTendril)
        {
            push = direction * knockback;
        }
        else
        {
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float side = Vector2.Dot(playerPos - (Vector2)basePos, perpendicular) >= 0f ? 1f : -1f;
            push = perpendicular * side * knockback;
        }

        return push + Vector2.up * knockback * knockbackUp;
    }
}

using UnityEngine;

// Anything poisonous the player can touch: puddles, slimy walls, spiked balls, drops, enemies.
// Needs a trigger Collider2D. Touching it poisons the player (refreshed while they stay in it).
// Optionally also deals a direct hit with knockback (that one respects dash and invincibility frames).
[RequireComponent(typeof(Collider2D))]
public class PoisonHazard : MonoBehaviour
{
    [SerializeField] int hitDamage = 0;          // direct damage on touch (0 = poison only)
    [SerializeField] float knockback = 0f;       // push away from this object when the hit lands
    [SerializeField] int poisonDamage = 2;       // damage per poison tick
    [SerializeField] float poisonDuration = 2f;  // how long the poison lasts after the last touch
    [SerializeField] float poisonTickInterval = 0.5f;
    [SerializeField] bool destroyOnHit;          // drops: disappear when they hit the player

    public void Setup(int hit, float push, int tick, float duration, float interval, bool destroy)
    {
        hitDamage = hit;
        knockback = push;
        poisonDamage = tick;
        poisonDuration = duration;
        poisonTickInterval = interval;
        destroyOnHit = destroy;
    }

    void OnTriggerEnter2D(Collider2D other) => Touch(other);
    void OnTriggerStay2D(Collider2D other)  => Touch(other);

    void Touch(Collider2D other)
    {
        if (!enabled) return;
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;

        if (hitDamage > 0)
        {
            int before = health.CurrentHealth;
            health.TakeDamage(hitDamage);
            if (health.CurrentHealth < before && knockback > 0f && other.attachedRigidbody)
            {
                Vector2 away = (Vector2)(other.transform.position - transform.position);
                if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                away.Normalize();
                away.y = Mathf.Max(away.y, 0.4f); // always a little upward
                other.attachedRigidbody.linearVelocity = away.normalized * knockback;
            }
        }

        Poison.Apply(other.gameObject, poisonDamage, poisonDuration, poisonTickInterval);
        if (destroyOnHit) Destroy(gameObject);
    }
}

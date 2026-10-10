using UnityEngine;

// A hitbox (Collider2D with Is Trigger ON). When the player is inside it: damage, knockback, and optional fire.
// Bosses create these for their attacks and set the values, but you can also put one on any object.
public class DamageZone : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] float knockback = 8f;
    [SerializeField] float knockbackUp = 0.5f;

    [Header("Fire")]
    [SerializeField] bool burns;
    [SerializeField] int burnDamage = 1;
    [SerializeField] float burnDuration = 3f;
    [SerializeField] float burnTickInterval = 1f;

    public bool Active { get; set; } = true;

    public void Setup(int damage, float knockback, bool burns, int burnDamage, float burnDuration, float burnTickInterval)
    {
        this.damage = damage;
        this.knockback = knockback;
        this.burns = burns;
        this.burnDamage = burnDamage;
        this.burnDuration = burnDuration;
        this.burnTickInterval = burnTickInterval;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!Active) return;
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;

        int before = health.CurrentHealth;
        health.TakeDamage(damage);
        if (health.CurrentHealth >= before) return; // dashing or invincible: nothing landed

        if (other.attachedRigidbody)
        {
            float dir = Mathf.Sign(other.transform.position.x - transform.position.x);
            Vector2 push = new Vector2(dir, knockbackUp).normalized * knockback;
            Rigidbody2D prb = other.attachedRigidbody;
            prb.linearVelocity = new Vector2(push.x, Mathf.Max(prb.linearVelocity.y, push.y)); // never cancels a jump in progress
        }
        if (burns) Burn.Apply(other.gameObject, burnDamage, burnDuration, burnTickInterval);
    }
}

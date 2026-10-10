using UnityEngine;

// Ek hitbox (Collider2D Is Trigger ON). Player isme aaye toh damage, dhakka, aur chaho toh aag.
// Boss apne attacks ke liye ye khud bana ke values set karta hai, par akele bhi kisi object pe laga sakte ho.
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
        if (health.CurrentHealth >= before) return; // dash ya invincible: kuch nahi laga

        if (other.attachedRigidbody)
        {
            float dir = Mathf.Sign(other.transform.position.x - transform.position.x);
            other.attachedRigidbody.linearVelocity = new Vector2(dir, knockbackUp).normalized * knockback;
        }
        if (burns) Burn.Apply(other.gameObject, burnDamage, burnDuration, burnTickInterval);
    }
}

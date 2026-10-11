using UnityEngine;

public class ContactDamage : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] Vector2 knockback = new Vector2(8f, 6f);

    void OnCollisionStay2D(Collision2D collision)
    {
        // Only damage the player
        if (!collision.gameObject.TryGetComponent(out PlayerController2D _)) return;
        if (!collision.gameObject.TryGetComponent(out Health health)) return;

        int before = health.CurrentHealth;
        health.TakeDamage(damage);

        // Knock back only if the damage actually landed
        if (health.CurrentHealth < before && collision.rigidbody)
        {
            float dir = Mathf.Sign(collision.transform.position.x - transform.position.x);
            collision.rigidbody.linearVelocity = new Vector2(dir * knockback.x, knockback.y);
        }
    }
}
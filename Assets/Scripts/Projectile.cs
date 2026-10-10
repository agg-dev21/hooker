using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 8f;
    [SerializeField] int damage = 1;
    [SerializeField] float lifetime = 4f; // disappears after this many seconds
    [SerializeField] LayerMask groundLayer;

    // The shooter calls this to fire the projectile
    public void Launch(Vector2 direction)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = direction.normalized * speed;
        transform.right = direction; // point the projectile in its direction
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hit the player: deal damage and disappear
        if (other.TryGetComponent(out PlayerController2D _) && other.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
        }
        // Hit the ground or a wall: disappear
        else if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }
    }
}
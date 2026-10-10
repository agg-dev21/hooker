using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 8f;
    [SerializeField] int damage = 1;
    [SerializeField] float lifetime = 4f; // itne second baad apne aap gayab
    [SerializeField] LayerMask groundLayer;

    // Shooter ye call karta hai goli chalane ke liye
    public void Launch(Vector2 direction)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = direction.normalized * speed;
        transform.right = direction; // goli us direction mein face kare
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Player ko lagi: damage do aur goli gayab
        if (other.TryGetComponent(out PlayerController2D _) && other.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
        }
        // Zameen ya deewar ko lagi: goli gayab
        else if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
        }
    }
}
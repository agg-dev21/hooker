using System.Collections;
using UnityEngine;

// Barrel: player chhoota hai toh fuse jalta hai (chamakta hai), thodi der baad BOOM.
// Paas mein jiske paas Health hai (player, crates) sabko damage. Player ko dhakka bhi.
// Object pe SpriteRenderer + Collider2D (solid) chahiye.
public class ExplodingBarrel : MonoBehaviour
{
    [SerializeField] float fuseTime = 0.8f;     // chhoone ke baad kitni der mein phate
    [SerializeField] float radius = 3f;
    [SerializeField] int damage = 3;
    [SerializeField] float knockback = 14f;
    [SerializeField] float knockbackUp = 0.5f;    // dhakke mein upar kitna
    [SerializeField] float fuseFlashSpeed = 10f;
    [SerializeField] float boomShowTime = 0.15f;  // orange flash kitni der dikhe
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] Color boomColor = new Color(1f, 0.55f, 0.1f);

    bool lit;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!lit && collision.gameObject.TryGetComponent(out PlayerController2D _))
            StartCoroutine(Fuse());
    }

    IEnumerator Fuse()
    {
        lit = true;
        Color original = sr ? sr.color : Color.white;

        float t = 0f;
        while (t < fuseTime)
        {
            t += Time.deltaTime;
            if (sr) sr.color = Mathf.PingPong(t * fuseFlashSpeed, 1f) > 0.5f ? flashColor : original;
            yield return null;
        }

        Explode();
    }

    void Explode()
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, radius))
        {
            if (hit.gameObject == gameObject || !hit.TryGetComponent(out Health health)) continue;

            health.TakeDamage(damage);

            // Player ko barrel se door uchhalo
            if (hit.TryGetComponent(out PlayerController2D _) && hit.attachedRigidbody)
            {
                Vector2 away = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                hit.attachedRigidbody.linearVelocity = (away + Vector2.up * knockbackUp).normalized * knockback;
            }
        }

        // Simple boom: bada orange flash, phir gayab
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (sr) sr.color = boomColor;
        transform.localScale = Vector3.one * radius * 2f;
        Destroy(gameObject, boomShowTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

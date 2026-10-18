using System.Collections;
using UnityEngine;

// Barrel: when the player touches it, the fuse lights (it flashes), and a moment later it explodes.
// Damages everything nearby that has Health (player, crates) and knocks the player back.
// Needs a SpriteRenderer and a solid Collider2D.
public class ExplodingBarrel : MonoBehaviour
{
    [SerializeField] float fuseTime = 0.8f;     // seconds from touch to explosion
    [SerializeField] float radius = 3f;
    [SerializeField] int damage = 20;
    [SerializeField] float knockback = 14f;
    [SerializeField] float knockbackUp = 0.5f;    // how much of the knockback goes upward
    [SerializeField] float fuseFlashSpeed = 10f;
    [SerializeField] float boomShowTime = 0.15f;  // how long the orange blast stays visible
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

            // Launch the player away from the barrel
            if (hit.TryGetComponent(out PlayerController2D _) && hit.attachedRigidbody)
            {
                Vector2 away = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                hit.attachedRigidbody.linearVelocity = (away + Vector2.up * knockbackUp).normalized * knockback;
            }
        }

        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;

        // The blast drawing, standing on the ground under the barrel
        GroundFxArt fx = GroundFxArt.Current;
        if (fx && fx.HasBarrelBlast)
        {
            float bottom = sr ? sr.bounds.min.y : transform.position.y;
            GroundFxArt.Play(fx.barrelBlastFrames, new Vector2(transform.position.x, bottom), radius * 2f, fx.fps);
            Destroy(gameObject);
            return;
        }

        // No drawing: simple blast, a big orange flash, then gone
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

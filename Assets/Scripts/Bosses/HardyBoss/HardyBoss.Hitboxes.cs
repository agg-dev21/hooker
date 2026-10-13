using System.Collections;
using UnityEngine;

// Part of HardyBoss (see HardyBoss.cs for its settings and the main loop).
// Building blocks the attacks use: hitboxes, sprite pieces and clean-up.
public partial class HardyBoss
{
    // ---------------- Helpers ----------------
    // Damages the player if they are inside the box. Returns true if damage landed.
    bool HitBox(Vector2 center, Vector2 size, int damage, float knockback, bool burns)
    {
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;

            int before = h.CurrentHealth;
            h.TakeDamage(damage);
            if (h.CurrentHealth >= before) return false; // dashing or invincible: nothing landed

            if (hit.attachedRigidbody)
            {
                float dir = Mathf.Sign(hit.transform.position.x - rb.position.x); // always away from the boss
                if (dir == 0f) dir = sr && (artFacesRight ? !sr.flipX : sr.flipX) ? 1f : -1f; // exactly on top: push the way he faces
                Vector2 push = new Vector2(dir, 0.6f).normalized * knockback;
                Rigidbody2D prb = hit.attachedRigidbody;
                prb.linearVelocity = new Vector2(push.x, Mathf.Max(prb.linearVelocity.y, push.y)); // never cancels a jump in progress
            }
            if (burns) Burn.Apply(hit.gameObject, burnDamage, burnDuration, burnTickInterval);
            return true;
        }
        return false;
    }

    void HitCircle(Vector2 center, float radius, int damage, float knockback, bool burns)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
        {
            if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;

            int before = h.CurrentHealth;
            h.TakeDamage(damage);
            if (h.CurrentHealth >= before) continue;

            if (hit.attachedRigidbody)
            {
                float dir = Mathf.Sign(hit.transform.position.x - center.x);
                Vector2 push = new Vector2(dir, 0.6f).normalized * knockback;
                Rigidbody2D prb = hit.attachedRigidbody;
                prb.linearVelocity = new Vector2(push.x, Mathf.Max(prb.linearVelocity.y, push.y)); // never cancels a jump in progress
            }
            if (burns) Burn.Apply(hit.gameObject, burnDamage, burnDuration, burnTickInterval);
        }
    }

    // A box made from the boss's sprite. hurts = true adds a trigger + DamageZone
    GameObject MakePiece(string name, Color color, bool hurts)
    {
        var go = new GameObject(name);
        var pieceSr = go.AddComponent<SpriteRenderer>();
        pieceSr.sprite = blockSprite ? blockSprite : (sr ? sr.sprite : null);
        pieceSr.color = color;
        pieceSr.sortingOrder = 3;
        if (hurts)
        {
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            go.AddComponent<DamageZone>();
        }
        spawned.Add(go);
        return go;
    }

    static void SetSegment(GameObject go, Vector2 from, Vector2 to, float thickness)
    {
        go.transform.position = (from + to) * 0.5f;
        go.transform.localScale = new Vector3(Mathf.Max(Mathf.Abs(to.x - from.x), 0.001f), thickness, 1f);
    }

    static void SetBox(GameObject go, Vector2 center, Vector2 size)
    {
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    void Remove(GameObject go)
    {
        spawned.Remove(go);
        if (go) Destroy(go);
    }

    IEnumerator RemoveAfter(GameObject go, float time)
    {
        yield return new WaitForSeconds(time);
        Remove(go);
    }

    void Face(float dir)
    {
        if (sr && dir != 0f) sr.flipX = artFacesRight ? dir < 0f : dir > 0f;
    }
}

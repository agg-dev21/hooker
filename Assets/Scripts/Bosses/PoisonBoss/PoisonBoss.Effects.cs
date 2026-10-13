using System.Collections;
using UnityEngine;

// Part of PoisonBoss (see PoisonBoss.cs for its settings and the main loop).
// Warnings, tints, touch damage and the poison it leaves behind (puddles, splashes, drops).
public partial class PoisonBoss
{
    // Flash before an attack so the player can react
    IEnumerator Warn(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Tint(Mathf.PingPong(t * 8f, 1f) > 0.5f ? warnColor : bodyColor);
            yield return null;
        }
        Tint(bodyColor);
    }

    void Tint(Color c) { if (sr) sr.color = c; }

    void SetTouchDamage(int damage)
    {
        if (!touchHazard) return;
        // Turn the hitbox itself on/off (a switched-off script would still get touch events)
        if (touchHazard.TryGetComponent(out Collider2D touchCol)) touchCol.enabled = touchHurts;
        touchHazard.Setup(damage, touchKnockback, touchPoisons ? poisonDamage : 0, touchPoisons ? poisonDuration : 0f, 0.5f, false);
    }

    bool HitPlayer(Collider2D hit, int damage, Vector2 knockback, bool poisons = true)
    {
        if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) return false;
        int before = h.CurrentHealth;
        h.TakeDamage(damage);
        if (h.CurrentHealth >= before) return false; // dashing or invincible
        if (hit.attachedRigidbody) hit.attachedRigidbody.linearVelocity = knockback;
        if (poisons) Poison.Apply(hit.gameObject, poisonDamage, poisonDuration, 0.5f);
        return true;
    }

    // A poison puddle on the floor that disappears after a while
    void SpawnPuddle(float x, float width, float lifetime)
    {
        var go = new GameObject("BossPuddle");
        go.transform.position = new Vector2(x, groundY + 0.12f);
        go.transform.localScale = new Vector3(width, 0.25f, 1f);
        var psr = go.AddComponent<SpriteRenderer>();
        psr.sprite = sr ? sr.sprite : null;
        psr.color = poisonColor;
        psr.sortingOrder = 2;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 3f);
        col.offset = new Vector2(0f, 1f);
        go.AddComponent<PoisonHazard>().Setup(0, 0f, poisonDamage, 2f, 0.5f, false); // also puts on the puddle drawing

        // The game-wide poison art (Assets/Resources/PoisonArt) is used when it exists
        if (PoisonArt.Current)
        {
            if (splashOnPuddle) PoisonArt.SplashAt(new Vector2(x, groundY), Mathf.Min(width, 4f));
        }
        // Otherwise the lizard's own poison frames: hide the plain box, show the puddle drawing (and a splash when it appears)
        else if (poisonFrames != null && poisonFrames.Length > 0)
        {
            psr.enabled = false;
            Sprite puddle = poisonFrames[Mathf.Clamp(puddleFrame, 0, poisonFrames.Length - 1)];
            var art = new GameObject("PuddleArt");
            art.transform.SetParent(go.transform, false);
            art.transform.localPosition = new Vector3(0f, -0.5f, 0f); // bottom of the puddle box
            Vector2 drawn = puddle.bounds.size;
            art.transform.localScale = new Vector3(1f / Mathf.Max(drawn.x, 0.01f), puddleArtHeight / Mathf.Max(drawn.y, 0.01f) / 0.25f, 1f);
            var asr = art.AddComponent<SpriteRenderer>();
            asr.sprite = puddle;
            asr.sortingOrder = 2;
            if (splashOnPuddle) StartCoroutine(Splash(new Vector2(x, groundY)));
        }
        Destroy(go, lifetime);
        spawned.Add(go);
    }

    // The poison splash animation, played once
    IEnumerator Splash(Vector2 at)
    {
        var fx = new GameObject("PoisonSplash");
        fx.transform.position = at;
        var fsr = fx.AddComponent<SpriteRenderer>();
        fsr.sortingOrder = 4;
        spawned.Add(fx);
        foreach (Sprite f in poisonFrames)
        {
            if (!fx) yield break; // cleared (fight reset or boss died)
            fsr.sprite = f;
            yield return new WaitForSeconds(1f / Mathf.Max(splashFps, 0.01f));
        }
        spawned.Remove(fx);
        Destroy(fx);
    }

    void SpawnDrop(Vector2 pos)
    {
        var drop = new GameObject("BossRainDrop");
        drop.transform.position = pos;
        drop.transform.localScale = Vector3.one * 0.05f;
        var dsr = drop.AddComponent<SpriteRenderer>();
        dsr.sprite = sr ? sr.sprite : null;
        dsr.color = poisonColor;
        dsr.sortingOrder = 3;
        var col = drop.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        drop.AddComponent<PoisonHazard>().Setup(4, 0f, poisonDamage, poisonDuration, 0.5f, true);
        drop.AddComponent<PoisonDrop>().Setup(0.5f, 0.5f, 12f, 40f, LayerMask.GetMask("Ground"));
        spawned.Add(drop);
    }

    void ClearSpawned()
    {
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }
}

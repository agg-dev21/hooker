using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Part of OctopusBoss (see OctopusBoss.cs for its settings and the main loop).
// Every attack: Lash, Chain Rain, Grab, Anchor, Chain Bind and Throw.
public partial class OctopusBoss
{
    // ---------------- 1 + 2) Lashes ----------------

    IEnumerator Lash(float heightAboveFloor)
    {
        float y = groundY + heightAboveFloor;
        Vector2 from = new Vector2(HandPos.x, y);
        float length = arenaMaxX - from.x;

        GameObject warn = Bar(from, Vector2.right, length, 0.5f, warnColor, false);
        yield return Flash(lashWarnTime / Speed);
        Destroy(warn);

        Chain chain = new Chain(this, from, Vector2.right, 0.45f, lashDamage, 10f);
        yield return chain.Extend(length, lashSpeed * Speed);
        yield return new WaitForSeconds(0.15f);
        yield return chain.Retract(lashSpeed * 0.6f);
    }

    // ---------------- 3) Chain Rain ----------------

    IEnumerator ChainRain()
    {
        int count = phase2 ? rainChains + 2 : rainChains;
        var xs = new List<float>();
        if (player) xs.Add(Mathf.Clamp(player.position.x, rainMinX, rainMaxX)); // one always aimed at you
        while (xs.Count < count)
        {
            float x = Random.Range(rainMinX, rainMaxX);
            bool tooClose = false;
            foreach (float other in xs) if (Mathf.Abs(other - x) < 2.5f) tooClose = true;
            if (!tooClose) xs.Add(x);
        }

        var warns = new List<GameObject>();
        foreach (float x in xs) warns.Add(Bar(new Vector2(x, ceilingY), Vector2.down, ceilingY - groundY, 0.6f, warnColor, false));
        yield return Flash(rainWarnTime / Speed);
        foreach (GameObject w in warns) Destroy(w);

        var chains = new List<Chain>();
        foreach (float x in xs) chains.Add(new Chain(this, new Vector2(x, ceilingY), Vector2.down, 0.55f, rainDamage, 8f));
        float len = 0f, full = ceilingY - groundY;
        while (len < full)
        {
            len = Mathf.MoveTowards(len, full, 45f * Speed * Time.deltaTime);
            foreach (Chain c in chains) c.SetLength(len);
            yield return null;
        }
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.2f, 0.2f);
        yield return new WaitForSeconds(0.35f);
        while (len > 0f)
        {
            len = Mathf.MoveTowards(len, 0f, 18f * Time.deltaTime);
            foreach (Chain c in chains) c.SetLength(len);
            yield return null;
        }
        foreach (Chain c in chains) c.Destroy();
    }

    // ---------------- 4) Chain Grab ----------------

    IEnumerator Grab()
    {
        Vector2 from = HandPos;
        Vector2 dir = ((Vector2)player.position - from).normalized;

        // Aim line follows you for a moment, then locks
        GameObject warn = Bar(from, dir, grabRange, 0.25f, warnColor, false);
        for (float t = 0f; t < grabWarnTime / Speed; t += Time.deltaTime)
        {
            if (t < grabWarnTime / Speed * 0.6f && player)
            {
                dir = ((Vector2)player.position - from).normalized;
                warn.transform.right = dir;
            }
            FlashParts(Mathf.PingPong(t * 8f, 1f) > 0.5f);
            yield return null;
        }
        FlashParts(false);
        Destroy(warn);

        Chain chain = new Chain(this, from, dir, 0.4f, 0, 0f); // damage handled below (it pulls you instead of pushing)
        bool caught = false;
        float len = 0f;
        while (len < grabRange && !caught)
        {
            len = Mathf.MoveTowards(len, grabRange, grabSpeed * Speed * Time.deltaTime);
            chain.SetLength(len);
            Vector2 tip = from + dir * len;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(tip, 0.6f))
            {
                if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;
                int before = h.CurrentHealth;
                h.TakeDamage(grabDamage);
                if (h.CurrentHealth < before && playerRb)
                {
                    caught = true;
                    Vector2 pull = (from - (Vector2)hit.transform.position).normalized;
                    playerRb.linearVelocity = new Vector2(pull.x, Mathf.Max(pull.y, 0.3f)) * grabPull;
                }
                break;
            }
            if (Physics2D.OverlapPoint(tip, LayerMask.GetMask("Ground"))) break; // hit a wall
            yield return null;
        }

        if (caught && player)
        {
            // After the yank: held in place, and the lizard gets a free shot
            yield return new WaitForSeconds(grabPullTime);
            if (player.TryGetComponent(out PlayerController2D pc)) pc.Root(grabHoldTime);
            if (lizardVomitsOnGrab)
                foreach (PoisonBoss lizard in FindObjectsByType<PoisonBoss>())
                    lizard.VomitAt(player.position);
        }
        yield return chain.Retract(30f);
    }

    // ---------------- 5) Anchor ----------------

    IEnumerator Anchor()
    {
        yield return Flash(0.5f / Speed);
        Vector2 from = HandPos + Vector2.up;
        float landX = player ? player.position.x : (rainMinX + rainMaxX) * 0.5f;
        Vector2 to = new Vector2(landX, groundY + 0.6f);

        GameObject mark = Bar(new Vector2(landX - anchorRadius, groundY + 0.15f), Vector2.right, anchorRadius * 2f, 0.3f, warnColor, false);
        GameObject anchor = Bar(from, Vector2.right, 1.2f, 1.2f, chainColor, false);
        float time = anchorAirTime / Speed;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            Vector2 p = Vector2.Lerp(from, to, k) + Vector2.up * (4f * anchorHeight * k * (1f - k));
            anchor.transform.position = p - Vector2.right * 0.6f;
            anchor.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
            yield return null;
        }
        Destroy(mark);
        Destroy(anchor);

        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.3f, 0.35f);
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(to, anchorRadius))
        {
            if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;
            int before = h.CurrentHealth;
            h.TakeDamage(anchorDamage);
            if (h.CurrentHealth < before && hit.attachedRigidbody)
                hit.attachedRigidbody.linearVelocity = new Vector2(Mathf.Sign(hit.transform.position.x - landX), 0.8f) * 12f;
            break;
        }
    }

    // ---------------- A) Chain Bind ----------------

    IEnumerator Bind()
    {
        PlayAttackPose();
        // A red mark on the floor follows the player, then locks
        float FloorUnder(float x)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, player.position.y + 0.5f), Vector2.down, 30f, LayerMask.GetMask("Ground"));
            return hit ? hit.point.y : groundY;
        }
        Vector2 spot = new Vector2(player.position.x, FloorUnder(player.position.x));
        GameObject mark = Bar(spot - Vector2.right * bindRadius, Vector2.right, bindRadius * 2f, 0.3f, warnColor, false);
        for (float t = 0f; t < bindWarnTime / Speed; t += Time.deltaTime)
        {
            spot = new Vector2(player.position.x, FloorUnder(player.position.x));
            mark.transform.position = spot - Vector2.right * bindRadius;
            FlashParts(Mathf.PingPong(t * 8f, 1f) > 0.5f);
            yield return null;
        }
        FlashParts(false);
        yield return new WaitForSeconds(bindLockTime / Speed);
        Destroy(mark);

        // Chains burst out of the ground
        GameObject fx = null;
        SpriteRenderer fsr = null;
        if (bindFrames != null && bindFrames.Length > 0)
        {
            fx = new GameObject("ChainBind");
            fx.transform.position = spot;
            fsr = fx.AddComponent<SpriteRenderer>();
            fsr.sortingOrder = 9;
            spawned.Add(fx);
        }
        int frames = fsr ? bindFrames.Length : 1;
        for (int f = 0; f < frames; f++)
        {
            if (fsr) fsr.sprite = bindFrames[f];
            if (f == Mathf.Min(bindHitFrame, frames - 1)) TryBind(spot);
            yield return new WaitForSeconds(1f / bindFps);
        }
        if (fx) { spawned.Remove(fx); Destroy(fx, 0.3f); }
    }

    // Caught inside the burst (and not dashing): damage, held in place, Oscar gets his shot
    void TryBind(Vector2 spot)
    {
        if (!player || Mathf.Abs(player.position.x - spot.x) > bindRadius || player.position.y - spot.y > 3f) return;
        if (!player.TryGetComponent(out Health h) || h.IsInvincible) return;
        h.TakeDamage(bindDamage);
        if (player.TryGetComponent(out PlayerController2D pc)) pc.Root(grabHoldTime);
        if (lizardVomitsOnGrab)
            foreach (PoisonBoss lizard in FindObjectsByType<PoisonBoss>())
                lizard.VomitAt(player.position);
    }

    // ---------------- B) Chain Throw ----------------

    IEnumerator Throw()
    {
        yield return Flash(throwWarnTime / Speed);
        PlayAttackPose();
        yield return new WaitForSeconds(0.15f);
        int count = phase2 ? Mathf.Max(3, throwChains) : throwChains;
        Vector2 from = HandPos;
        Vector2 aim = ((Vector2)player.position - from).normalized;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * throwSpread;
            StartCoroutine(FlyingChain(from, Quaternion.Euler(0f, 0f, angle) * aim));
        }
        yield return new WaitForSeconds(0.4f);
    }

    IEnumerator FlyingChain(Vector2 from, Vector2 dir)
    {
        var go = new GameObject("ThrownChain");
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        if (chainSprite) sr.sprite = chainSprite;
        else { sr.sprite = boxSprite; sr.color = chainColor; go.transform.localScale = new Vector3(0.25f, 1.6f, 1f); }
        go.transform.up = dir; // the chain is drawn standing up: point it where it flies
        spawned.Add(go);

        int ground = LayerMask.GetMask("Ground");
        for (float t = 0f; t < 4f && go; t += Time.deltaTime)
        {
            Vector2 p = (Vector2)go.transform.position + dir * throwSpeed * Speed * Time.deltaTime;
            go.transform.position = p;
            if (Physics2D.OverlapPoint(p, ground)) break; // hit a wall or the floor
            if (player && Vector2.Distance(p, player.position) < 0.9f)
            {
                if (player.TryGetComponent(out Health h) && !h.IsInvincible)
                {
                    h.TakeDamage(throwDamage);
                    if (playerRb) playerRb.linearVelocity = dir * 8f + Vector2.up * 3f;
                }
                break;
            }
            yield return null;
        }
        if (go) { spawned.Remove(go); Destroy(go); }
    }

    void PlayAttackPose()
    {
        animTime = 0f;
        attackPoseUntil = Time.time + (attackFrames != null ? attackFrames.Length / Mathf.Max(attackFps, 0.01f) : 0f);
    }
}

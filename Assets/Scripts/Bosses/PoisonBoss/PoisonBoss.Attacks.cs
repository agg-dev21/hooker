using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Part of PoisonBoss (see PoisonBoss.cs for its settings and the main loop).
// Every attack: Charge, Vomit, Glob, Claws, mast attack, Slam, Rain and Spit.
public partial class PoisonBoss
{
    // ---------------- 1) Charge ----------------

    bool dashHit; // the last dash ran into the player

    IEnumerator Charge()
    {
        float dir = DirToPlayer;
        float groundCenter = startPos.y;
        Sprite[] spin = Part(dashFrames, dashWindupFrames, (dashFrames != null ? dashFrames.Length : 0) - dashWindupFrames);
        Face(dir);
        yield return Windup(dashFrames, dashWindupFrames, dashFps, chargeWarnTime / Speed);

        // 1) Low dash along the floor (spinning)
        Play(spin, dashFps, true);
        yield return DashAcross(dir, true);
        if (dashHit) { PlayIdle(); yield break; }
        PlayIdle();
        yield return Crash();
        if (!useLedgeDash) yield break;

        // 2) Climb onto the ledge on this side, dash across up there the other way, crash again
        yield return MoveToHeight(ledgeTopY + halfHeight, climbTime);
        Face(-dir);
        if (HasArt) yield return new WaitForSeconds(ledgeDashWarnTime / Speed);
        else yield return Warn(ledgeDashWarnTime / Speed);
        Play(spin, dashFps, true);
        yield return DashAcross(-dir, false);
        PlayIdle();
        if (!dashHit) yield return Crash();

        // 3) Back down to the floor
        yield return MoveToHeight(groundCenter, dropTime);
    }

    // Dash to the wall in this direction at his current height. Stops early if it runs into the player (dashHit).
    IEnumerator DashAcross(float dir, bool trail)
    {
        dashHit = false;
        float targetX = dir > 0 ? arenaMaxX - halfWidth : arenaMinX + halfWidth;
        float nextTrail = rb.position.x;
        while (Mathf.Abs(rb.position.x - targetX) > 0.05f)
        {
            float x = Mathf.MoveTowards(rb.position.x, targetX, chargeSpeed * Speed * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, rb.position.y));
            if (trail && phase2 && phase2TrailGap > 0f && Mathf.Abs(x - nextTrail) >= phase2TrailGap) // phase 2: poison trail behind him
            {
                nextTrail = x;
                SpawnPuddle(x, 2.5f, puddleLifetime);
            }

            // Ran into the player: big hit, and the dash stops there
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(new Vector2(x, rb.position.y), new Vector2(halfWidth * 2f, halfHeight * 2f), 0f))
                if (HitPlayer(hit, dashHitDamage, new Vector2(dir, 0.6f).normalized * dashKnockback, false)) { dashHit = true; break; }
            if (dashHit) yield break;

            yield return new WaitForFixedUpdate();
        }
    }

    // He slams into the wall, is stunned, and poison bursts all over the arena
    IEnumerator Crash()
    {
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.45f, 0.45f);
        for (int i = 0; i < crashPuddles; i++)
            SpawnPuddle(Random.Range(arenaMinX + 1f, arenaMaxX - 1f), Random.Range(2f, 3.5f), puddleLifetime);
        StartCoroutine(DropsAllOver(crashDrops, 1.2f));
        Tint(Color.Lerp(bodyColor, Color.gray, 0.5f)); // dazed
        yield return new WaitForSeconds(crashStunTime);
        Tint(bodyColor);
    }

    // Climb up or drop down to this height (his center) over a short time
    IEnumerator MoveToHeight(float centerY, float time)
    {
        Vector2 from = rb.position;
        for (float t = 0f; t < time; t += Time.fixedDeltaTime)
        {
            rb.MovePosition(new Vector2(from.x, Mathf.Lerp(from.y, centerY, t / time)));
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(new Vector2(from.x, centerY));
        yield return new WaitForFixedUpdate();
    }

    // Poison drops falling at random spots across the arena over a short time
    IEnumerator DropsAllOver(int count, float overTime)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnDrop(new Vector2(Random.Range(arenaMinX + 0.5f, arenaMaxX - 0.5f), ceilingY - 0.5f));
            yield return new WaitForSeconds(overTime / Mathf.Max(count, 1));
        }
    }

    // ---------------- 4) Vomit ----------------

    IEnumerator Vomit()
    {
        float dir = DirToPlayer;
        Face(dir);
        yield return Windup(spewFrames, spewWindupFrames, spewFps, vomitWarnTime / Speed);
        Play(Part(spewFrames, spewWindupFrames, (spewFrames != null ? spewFrames.Length : 0) - spewWindupFrames), spewFps, true);
        Vector2 mouth = rb.position + new Vector2(dir * halfWidth, halfHeight * 0.4f);
        int globs = phase2 ? vomitGlobs + 3 : vomitGlobs;
        for (int i = 0; i < globs; i++)
        {
            float angle = Mathf.Lerp(5f, vomitArc, globs > 1 ? i / (float)(globs - 1) : 0.5f);
            Vector2 v = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * dir, Mathf.Sin(angle * Mathf.Deg2Rad));
            float speed = vomitSpeed * Random.Range(0.75f, 1.15f);
            StartCoroutine(Glob(mouth, v * speed));
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(0.2f);
        PlayIdle();
    }

    // Level 3: the Siren chained the player in place, so he vomits right at them (it lands)
    public void VomitAt(Vector2 target)
    {
        if (!isActiveAndEnabled || !health || health.CurrentHealth <= 0) return;
        StartCoroutine(AimedVomit(target));
    }

    IEnumerator AimedVomit(Vector2 target)
    {
        float dir = target.x < rb.position.x ? -1f : 1f;
        Face(dir);
        Play(Part(spewFrames, spewWindupFrames, (spewFrames != null ? spewFrames.Length : 0) - spewWindupFrames), spewFps, true);
        yield return new WaitForSeconds(aimedVomitDelay);
        Vector2 mouth = rb.position + new Vector2(dir * halfWidth, halfHeight * 0.4f);
        float g = Mathf.Abs(Physics2D.gravity.y) * 1.5f; // same fall as the normal globs
        for (int i = 0; i < aimedVomitGlobs; i++)
        {
            // Arc that lands on the target after the flight time (a little spread so it looks like vomit)
            float time = Mathf.Max(0.2f, aimedVomitFlightTime * Random.Range(0.9f, 1.1f));
            Vector2 to = target + new Vector2(Random.Range(-0.25f, 0.25f), 0f) - mouth;
            Vector2 v = new Vector2(to.x / time, (to.y + 0.5f * g * time * time) / time);
            StartCoroutine(Glob(mouth, v));
            yield return new WaitForSeconds(0.08f);
        }
        yield return new WaitForSeconds(0.3f);
        PlayIdle();
    }

    // One poison glob: flies in an arc, hurts and poisons the player, and leaves a puddle where it lands
    IEnumerator Glob(Vector2 from, Vector2 velocity)
    {
        var glob = new GameObject("VomitGlob");
        glob.transform.position = from;
        glob.transform.localScale = Vector3.one * vomitGlobSize;
        var gsr = glob.AddComponent<SpriteRenderer>();
        gsr.sprite = sr ? sr.sprite : null;
        gsr.color = poisonColor;
        gsr.sortingOrder = 3;
        var col = glob.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        glob.AddComponent<PoisonHazard>().Setup(vomitDamage, 6f, poisonDamage, poisonDuration, 0.5f, true);
        spawned.Add(glob);

        Vector2 pos = from;
        float g = Mathf.Abs(Physics2D.gravity.y) * 1.5f;
        while (glob && pos.y > groundY + vomitGlobSize * 0.5f)
        {
            velocity.y -= g * Time.deltaTime;
            pos += velocity * Time.deltaTime;
            glob.transform.position = pos;
            if (pos.x < arenaMinX || pos.x > arenaMaxX) break;
            yield return null;
        }
        if (glob)
        {
            SpawnPuddle(Mathf.Clamp(pos.x, arenaMinX + 1f, arenaMaxX - 1f), vomitPuddleWidth, puddleLifetime);
            spawned.Remove(glob);
            Destroy(glob);
        }
    }

    // ---------------- 5) Claws ----------------

    IEnumerator Claws()
    {
        float dir = DirToPlayer;
        // Step in until the player is within reach (or time runs out)
        for (float t = 0f; t < clawApproachTime; t += Time.fixedDeltaTime)
        {
            float gap = Mathf.Abs(player.position.x - rb.position.x) - halfWidth;
            if (gap <= clawReach * 0.8f) break;
            dir = DirToPlayer;
            float x = Mathf.Clamp(rb.position.x + dir * clawStepSpeed * Speed * Time.fixedDeltaTime, arenaMinX + halfWidth, arenaMaxX - halfWidth);
            rb.MovePosition(new Vector2(x, rb.position.y));
            yield return new WaitForFixedUpdate();
        }

        for (int s = 0; s < clawSwipes; s++)
        {
            dir = DirToPlayer;
            yield return Warn(clawWindup / Speed);
            Vector2 center = new Vector2(rb.position.x + dir * (halfWidth + clawReach * 0.5f), groundY + clawHeight * 0.5f);
            bool landed = false;
            for (float t = 0f; t < clawSwipeTime && !landed; t += Time.deltaTime)
            {
                foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, new Vector2(clawReach, clawHeight), 0f))
                    if (HitPlayer(hit, clawDamage, new Vector2(dir, 0.5f).normalized * clawKnockback, false)) { landed = true; break; }
                yield return null;
            }
        }
    }

    // ---------------- Masts ----------------

    // Walk to the mast, then hit it until it breaks
    IEnumerator AttackMast(BreakableMast mast)
    {
        float targetX = Mathf.Clamp(mast.X, arenaMinX + halfWidth, arenaMaxX - halfWidth);
        while (!mast.Broken && Mathf.Abs(rb.position.x - targetX) > 0.1f)
        {
            float x = Mathf.MoveTowards(rb.position.x, targetX, mastWalkSpeed * Speed * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, rb.position.y));
            yield return new WaitForFixedUpdate();
        }
        for (int guard = 0; guard < 20 && !mast.Broken; guard++)
        {
            yield return Warn(mastHitWindup / Speed);
            mast.TakeHit();
            if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.12f, 0.15f);
            yield return new WaitForSeconds(mastHitInterval / Speed);
        }
    }

    // ---------------- Phase 2: poison everywhere ----------------

    IEnumerator PoisonEverywhere()
    {
        while (true)
        {
            for (int i = 0; i < phase2PuddlesPerWave; i++)
                SpawnPuddle(Random.Range(arenaMinX + 1f, arenaMaxX - 1f), Random.Range(2f, 3f), puddleLifetime);
            for (int i = 0; i < phase2DropsPerWave; i++)
                SpawnDrop(new Vector2(Random.Range(arenaMinX + 0.5f, arenaMaxX - 0.5f), ceilingY - 0.5f));
            yield return new WaitForSeconds(phase2PoisonInterval);
        }
    }

    // ---------------- 2) Slam ----------------

    IEnumerator Slam()
    {
        yield return Warn(0.4f / Speed);
        Vector2 from = rb.position;
        float landX = Mathf.Clamp(player.position.x, arenaMinX + halfWidth, arenaMaxX - halfWidth);
        float time = slamAirTime / Speed;
        for (float t = 0f; t < time; t += Time.fixedDeltaTime)
        {
            float k = t / time;
            float x = Mathf.Lerp(from.x, landX, k);
            float y = from.y + 4f * slamHeight * k * (1f - k); // arc up and down
            rb.MovePosition(new Vector2(x, y));
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(new Vector2(landX, from.y));
        yield return new WaitForFixedUpdate();

        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.35f, 0.4f);
        SpawnPuddle(landX, slamPuddleWidth * (phase2 ? 1.4f : 1f), puddleLifetime);
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(new Vector2(landX, groundY + 0.5f), slamRadius))
            if (HitPlayer(hit, slamDamage, new Vector2(Mathf.Sign(hit.transform.position.x - landX), 0.7f) * 12f)) break;
    }

    // ---------------- 3) Rain ----------------

    IEnumerator Rain()
    {
        yield return Warn(0.6f / Speed);
        int drops = phase2 ? Mathf.RoundToInt(rainDrops * 1.5f) : rainDrops;
        for (int i = 0; i < drops; i++)
        {
            float x = Random.Range(arenaMinX + 0.5f, arenaMaxX - 0.5f);
            if (i % 3 == 0 && player) x = Mathf.Clamp(player.position.x + Random.Range(-1.5f, 1.5f), arenaMinX, arenaMaxX); // some aimed at you
            SpawnDrop(new Vector2(x, ceilingY - 0.5f));
            yield return new WaitForSeconds(rainTime / drops / Speed);
        }
    }

    // ---------------- 4) Spit ----------------

    IEnumerator Spit()
    {
        yield return Warn(0.45f / Speed);
        if (!spitPrefab) yield break;
        int volleys = phase2 ? 2 : 1;
        for (int v = 0; v < volleys; v++)
        {
            Vector2 mouth = rb.position + Vector2.up * halfHeight * 0.4f;
            Vector2 aim = ((Vector2)player.position - mouth).normalized;
            for (int i = 0; i < spitCount; i++)
            {
                float angle = (i - (spitCount - 1) * 0.5f) * spitSpread;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * aim;
                Projectile p = Instantiate(spitPrefab, mouth + dir * (halfWidth + 0.3f), Quaternion.identity);
                if (p.TryGetComponent(out SpriteRenderer psr)) psr.color = poisonColor;
                p.Launch(dir);
            }
            yield return new WaitForSeconds(0.45f);
        }
    }
}

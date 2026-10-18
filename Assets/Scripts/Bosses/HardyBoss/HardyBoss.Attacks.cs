using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Part of HardyBoss (see HardyBoss.cs for its settings and the main loop).
// The four attacks: Tentacle Lash, Leap and Roll, Spikes and Punch.
public partial class HardyBoss
{
    // ---------------- 1) Tentacle Lash ----------------
    IEnumerator TentacleLash()
    {
        Face(DirToPlayer);
        if (tentaclePoseFrames != null && tentaclePoseFrames.Length > 0)
        {
            // Play the pose once, then hold its last frame while the tentacle is out
            Play(tentaclePoseFrames, tentaclePoseFps, false);
            yield return new WaitForSeconds(tentaclePoseFrames.Length / Mathf.Max(tentaclePoseFps, 0.01f));
        }
        else yield return Pose(tentaclePoseScale, tentaclePoseTime, tentacleAnimTrigger);

        float dir = DirToPlayer;
        Face(dir);
        Vector2 start = new Vector2(rb.position.x + dir * HalfSize.x, groundY + tentacleHeight);

        if (tentacleFxFrames != null && tentacleFxFrames.Length > 0)
        {
            yield return TentacleArt(start, dir);
            SetPose(Vector2.one);
            PlayIdle();
            yield break;
        }

        GameObject bodyPiece = MakePiece("TentacleBody", tentacleColor, true);
        GameObject tipPiece = MakePiece("TentacleTip", tentacleTipColor, true);
        bodyPiece.GetComponent<DamageZone>().Setup(tentacleBodyDamage, tentacleKnockback, tentacleBurns, burnDamage, burnDuration, burnTickInterval);
        tipPiece.GetComponent<DamageZone>().Setup(tentacleTipDamage, tentacleKnockback, tentacleBurns, burnDamage, burnDuration, burnTickInterval);

        float current = 0f;
        while (current < tentacleLength)
        {
            current = Mathf.MoveTowards(current, tentacleLength, tentacleExtendSpeed * Time.deltaTime);
            PlaceTentacle(bodyPiece, tipPiece, start, dir, current);
            yield return null;
        }
        yield return new WaitForSeconds(tentacleHoldTime);
        while (current > 0f)
        {
            current = Mathf.MoveTowards(current, 0f, tentacleRetractSpeed * Time.deltaTime);
            PlaceTentacle(bodyPiece, tipPiece, start, dir, current);
            yield return null;
        }

        Remove(bodyPiece);
        Remove(tipPiece);
        SetPose(Vector2.one);
    }

    // The tentacle animation. The drawing is stretched to Tentacle Length; the body and tip hitboxes
    // only hurt between Tentacle Hit First Frame and Tentacle Hit Last Frame.
    IEnumerator TentacleArt(Vector2 start, float dir)
    {
        var fx = new GameObject("TentacleFX");
        fx.transform.position = start;
        var fxSr = fx.AddComponent<SpriteRenderer>();
        fxSr.sortingOrder = 3;
        fxSr.sprite = tentacleFxFrames[0];
        float drawnLength = Mathf.Max(fxSr.sprite.bounds.size.x, 0.01f);
        // The drawing grows to the left from its pivot; flip it (negative x) to grow to the right
        fx.transform.localScale = new Vector3(-dir * tentacleLength / drawnLength, tentacleFxThicknessScale, 1f);
        spawned.Add(fx);

        GameObject bodyPiece = MakePiece("TentacleBodyHitbox", Color.clear, true);
        GameObject tipPiece = MakePiece("TentacleTipHitbox", Color.clear, true);
        var bodyZone = bodyPiece.GetComponent<DamageZone>();
        var tipZone = tipPiece.GetComponent<DamageZone>();
        bodyZone.Setup(tentacleBodyDamage, tentacleKnockback, tentacleBurns, burnDamage, burnDuration, burnTickInterval);
        tipZone.Setup(tentacleTipDamage, tentacleKnockback, tentacleBurns, burnDamage, burnDuration, burnTickInterval);
        PlaceTentacle(bodyPiece, tipPiece, start, dir, tentacleLength);

        float fps = Mathf.Max(tentacleFxFps, 0.01f);
        for (int i = 0; i < tentacleFxFrames.Length; i++)
        {
            fxSr.sprite = tentacleFxFrames[i];
            bool hurts = i >= tentacleHitFirstFrame && i <= tentacleHitLastFrame;
            bodyZone.Active = hurts;
            tipZone.Active = hurts;
            yield return new WaitForSeconds(1f / fps);
        }

        Remove(fx);
        Remove(bodyPiece);
        Remove(tipPiece);
    }

    void PlaceTentacle(GameObject bodyPiece, GameObject tipPiece, Vector2 start, float dir, float length)
    {
        float tip = Mathf.Min(length, tentacleTipLength);
        Vector2 split = start + Vector2.right * dir * (length - tip);
        SetSegment(bodyPiece, start, split, tentacleThickness);
        SetSegment(tipPiece, split, start + Vector2.right * dir * length, tentacleThickness * 1.2f);
    }

    // ---------------- 2) Leap + Roll ----------------
    // Parts of the jump slam sheet (empty if there is no art)
    Sprite[] JumpPart(int first, int count)
    {
        if (jumpSlamFrames == null || jumpSlamFrames.Length == 0) return null;
        first = Mathf.Clamp(first, 0, jumpSlamFrames.Length - 1);
        count = Mathf.Clamp(count, 1, jumpSlamFrames.Length - first);
        var part = new Sprite[count];
        System.Array.Copy(jumpSlamFrames, first, part, 0, count);
        return part;
    }

    IEnumerator LeapAndRoll()
    {
        Face(DirToPlayer);
        Sprite[] windup = JumpPart(0, jumpWindupFrames);
        Sprite[] spin = JumpPart(jumpSpinFirstFrame, jumpSpinFrameCount);
        Sprite[] land = JumpPart(jumpLandFrame, 1);
        if (windup != null)
        {
            Play(windup, jumpSlamFps, false);
            yield return new WaitForSeconds(windup.Length / Mathf.Max(jumpSlamFps, 0.01f));
            Play(spin, jumpSlamFps, true); // keeps spinning as long as the jump lasts
        }
        else
        {
            yield return Pose(leapPoseScale, leapPoseTime, leapAnimTrigger);
            SetPose(Vector2.one);
        }

        // Aim at the player's spot when he jumps (the player can still move away while he is in the air)
        float targetX = ClampX(player.position.x - DirToPlayer * leapLandOffset);
        Vector2 from = rb.position;
        Vector2 to = new Vector2(targetX, GroundCenterY);
        // Air time depends on how far he jumps, so the spin frames repeat more on long jumps
        float leapDuration = Mathf.Clamp(Mathf.Abs(to.x - from.x) / Mathf.Max(leapSpeed, 0.01f), leapMinTime, leapMaxTime);

        movingSelf = true;
        float t = 0f;
        while (t < leapDuration)
        {
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / leapDuration);
            Vector2 p = Vector2.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * leapHeight;
            rb.MovePosition(p);
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(to);
        movingSelf = false;

        // Landing shockwave (AOE)
        HitCircle(to, landingRadius, landingDamage, landingKnockback, landingBurns);
        GroundFxArt fx = GroundFxArt.Current;
        if (fx && fx.HasHardySlam) GroundFxArt.Play(fx.hardySlamFrames, new Vector2(to.x, groundY), landingRadius * 2f, fx.fps); // the slam drawing
        else
        {
            GameObject wave = MakePiece("Shockwave", shockwaveColor, false);
            wave.transform.position = new Vector3(to.x, groundY, 0f);
            wave.transform.localScale = new Vector3(landingRadius * 2f, landingRadius, 1f);
            StartCoroutine(RemoveAfter(wave, shockwaveShowTime));
        }

        // No roll: show the landing frame, then the attack is over
        if (!rollAfterLanding)
        {
            if (land != null)
            {
                Play(land, 1f, true);
                yield return new WaitForSeconds(jumpLandHoldTime);
            }
            PlayIdle();
            yield break;
        }

        // Roll toward the player
        float dir = DirToPlayer;
        Face(dir);
        if (land != null)
        {
            Play(land, 1f, true);
            yield return new WaitForSeconds(jumpLandHoldTime);
            Play(spin, jumpSlamFps, true); // keeps spinning as long as the roll lasts
        }
        else yield return Pose(rollPoseScale, rollPoseTime, rollAnimTrigger);

        GameObject rollZone = MakePiece("RollHitbox", Color.clear, true);
        rollZone.GetComponent<DamageZone>().Setup(rollDamage, rollKnockback, rollBurns, burnDamage, burnDuration, burnTickInterval);
        body.isTrigger = true; // while rolling the player can dash straight through
        movingSelf = true;

        float rolled = 0f;
        while (rolled < rollDistance)
        {
            float step = rollSpeed * Time.fixedDeltaTime;
            float nextX = ClampX(rb.position.x + dir * step);
            if (Mathf.Approximately(nextX, rb.position.x)) break; // reached the edge
            rolled += Mathf.Abs(nextX - rb.position.x);
            rb.MovePosition(new Vector2(nextX, GroundCenterY));
            SetBox(rollZone, new Vector2(nextX, GroundCenterY), HalfSize * 2f);
            yield return new WaitForFixedUpdate();
        }

        movingSelf = false;
        body.isTrigger = false;
        Remove(rollZone);
        SetPose(Vector2.one);
        if (land != null)
        {
            Play(land, 1f, true);
            yield return new WaitForSeconds(jumpLandHoldTime);
        }
        PlayIdle();
    }

    // ---------------- 3) Spikes ----------------
    IEnumerator Spikes()
    {
        Face(DirToPlayer);
        if (spikesPoseFrames != null && spikesPoseFrames.Length > 0)
        {
            // Play the pose once, then keep looping its last frames while the spikes erupt
            Play(spikesPoseFrames, spikesPoseFps, false);
            yield return new WaitForSeconds(spikesPoseFrames.Length / Mathf.Max(spikesPoseFps, 0.01f));
            int loopCount = Mathf.Clamp(spikesPoseLoopFrames, 1, spikesPoseFrames.Length);
            var tail = new Sprite[loopCount];
            System.Array.Copy(spikesPoseFrames, spikesPoseFrames.Length - loopCount, tail, 0, loopCount);
            Play(tail, spikesPoseFps, true);
        }
        else yield return Pose(spikesPoseScale, spikesPoseTime, spikesAnimTrigger);

        float dir = DirToPlayer;
        float x = rb.position.x + dir * (HalfSize.x + spikeStartDistance);
        for (int i = 0; i < spikeCount; i++)
        {
            if (x < arenaMinX || x > arenaMaxX) break;
            StartCoroutine(OneSpike(x));
            x += dir * spikeSpacing;
            yield return new WaitForSeconds(spikeInterval);
        }

        float spikeLife = spikeFxFrames != null && spikeFxFrames.Length > 0
            ? spikeFxFrames.Length / Mathf.Max(spikeFxFps, 0.01f)
            : spikeUpTime + spikeHeight / Mathf.Max(spikeRiseSpeed, 0.01f);
        yield return new WaitForSeconds(spikeLife);
        SetPose(Vector2.one);
        PlayIdle();
    }

    IEnumerator OneSpike(float x)
    {
        if (spikeFxFrames != null && spikeFxFrames.Length > 0)
        {
            yield return OneSpikeArt(x);
            yield break;
        }

        GameObject spike = MakePiece("Spike", spikeColor, true);
        spike.GetComponent<DamageZone>().Setup(spikeDamage, spikeKnockback, spikesBurn, burnDamage, burnDuration, burnTickInterval);

        float h = 0f;
        while (h < spikeHeight)
        {
            h = Mathf.MoveTowards(h, spikeHeight, spikeRiseSpeed * Time.deltaTime);
            SetBox(spike, new Vector2(x, groundY + h * 0.5f), new Vector2(spikeWidth, Mathf.Max(h, 0.01f)));
            yield return null;
        }
        yield return new WaitForSeconds(spikeUpTime);
        Remove(spike);
    }

    // A spike that plays the spike animation. The hitbox (Spike Width x Spike Height) only hurts
    // between Spike Hit First Frame and Spike Hit Last Frame.
    IEnumerator OneSpikeArt(float x)
    {
        var fx = new GameObject("SpikeFX");
        fx.transform.position = new Vector3(x, groundY, 0f);
        fx.transform.localScale = Vector3.one * spikeFxScale;
        var fxSr = fx.AddComponent<SpriteRenderer>();
        fxSr.sortingOrder = 3;
        spawned.Add(fx);

        GameObject hit = MakePiece("SpikeHitbox", Color.clear, true);
        SetBox(hit, new Vector2(x, groundY + spikeHeight * 0.5f), new Vector2(spikeWidth, spikeHeight));
        var zone = hit.GetComponent<DamageZone>();
        zone.Setup(spikeDamage, spikeKnockback, spikesBurn, burnDamage, burnDuration, burnTickInterval);

        float fps = Mathf.Max(spikeFxFps, 0.01f);
        for (int i = 0; i < spikeFxFrames.Length; i++)
        {
            fxSr.sprite = spikeFxFrames[i];
            zone.Active = i >= spikeHitFirstFrame && i <= spikeHitLastFrame;
            yield return new WaitForSeconds(1f / fps);
        }

        Remove(fx);
        Remove(hit);
    }

    // ---------------- Phase 2: random spikes all over the deck ----------------
    IEnumerator RandomSpikes()
    {
        while (true)
        {
            var used = new List<float>();
            for (int i = 0; i < randomSpikesPerWave; i++)
            {
                // Pick a spot that is not too close to the others in this wave
                float x = 0f;
                for (int tries = 0; tries < 10; tries++)
                {
                    x = Random.Range(arenaMinX + 1f, arenaMaxX - 1f);
                    bool clear = true;
                    foreach (float u in used) if (Mathf.Abs(u - x) < randomSpikeMinGap) { clear = false; break; }
                    if (clear) break;
                }
                used.Add(x);
                StartCoroutine(OneSpike(x));
            }
            yield return new WaitForSeconds(randomSpikeInterval);
        }
    }

    // ---------------- 4) Punch ----------------
    // How far the blade reaches from his center (his half width + Punch Range)
    float PunchReach => HalfSize.x + punchRange;

    // The hit area is a box from his center out to Punch Reach, Punch Hit Height tall, standing on the ground.
    // It starts at his center (not his edge) so a player standing inside or right next to him is hit too.
    IEnumerator Punch()
    {
        lastPunchSwung = false;

        // Step in first: he often stops walking further away than the blade reaches
        float t = 0f;
        while (player && Mathf.Abs(player.position.x - rb.position.x) > PunchReach - 0.3f && t < punchApproachTime)
        {
            walkDir = DirToPlayer;
            Face(walkDir);
            Play(walkFrames, walkFps, true);
            t += Time.deltaTime;
            yield return null;
        }
        walkDir = 0f;

        // Player got away: don't swing at the air, let the fight loop pick something else
        if (!player || Mathf.Abs(player.position.x - rb.position.x) > PunchReach)
        {
            PlayIdle();
            yield break;
        }

        lastPunchSwung = true;
        float dir = DirToPlayer;
        Face(dir);
        Vector2 boxCenter = new Vector2(rb.position.x + dir * PunchReach * 0.5f, groundY + punchHitHeight * 0.5f);
        Vector2 boxSize = new Vector2(PunchReach, punchHitHeight);
        bool landed = false;

        if (punchFrames != null && punchFrames.Length > 0)
        {
            Play(punchFrames, punchFps, false);
            float frameTime = 1f / Mathf.Max(punchFps, 0.01f);
            for (int i = 0; i < punchFrames.Length; i++)
            {
                bool active = i >= punchHitFirstFrame && i <= punchHitLastFrame;
                // Check for a hit every frame the blade is out (not just once per animation frame)
                for (float f = 0f; f < frameTime; f += Time.deltaTime)
                {
                    if (active && !landed) landed = HitBox(boxCenter, boxSize, punchDamage, punchKnockback, punchBurns);
                    yield return null;
                }
            }
        }
        else
        {
            // No art yet: the hit area is simply active for a moment
            for (float f = 0f; f < punchPlaceholderTime; f += Time.deltaTime)
            {
                if (!landed) landed = HitBox(boxCenter, boxSize, punchDamage, punchKnockback, punchBurns);
                yield return null;
            }
        }

        PlayIdle();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Level 2 boss: a poison lizard that rules the dungeon under the poison tower.
// Attacks (picked at random, never the same twice in a row; each can be switched off under "Attacks Used"):
//   1) Lizard Dash - glows, then dashes at the player. If it hits: big damage (no poison) and he stops.
//                    If the player dodges: he crashes into the wall, is stunned, and poison bursts all over the arena.
//                    Then he climbs onto the side ledge (the first level), dashes across at that height and
//                    crashes into the opposite side too (poison again), then drops back down.
//                    Phase 2: leaves a poison trail.
//   2) Slam        - leaps onto the player and lands with a poison splash. (off by default)
//   3) Rain        - poison drops fall from the ceiling all over the arena. (off by default)
//   4) Vomit       - sprays poison globs in an arc at the player; each leaves a puddle where it lands.
//   5) Claws       - when the player is close he steps in and swipes with his claws (off by default: he has contact damage instead).
// Below Phase 2 At he gets faster and angrier, and poison keeps spawning all over the arena.
// Masts: if the player camps on a mast's crow's nest, he walks over and hits the mast until it breaks.
// If the player dies he gets all his health back and the fight starts over.
// Needs: Health, a BoxCollider2D (his body) and a SpriteRenderer.
[RequireComponent(typeof(Health), typeof(BoxCollider2D))]
public class PoisonBoss : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] float groundY = 0f;     // top of the floor
    [SerializeField] float ceilingY = 19f;   // poison rain starts here
    [SerializeField] float arenaMinX = 1f;
    [SerializeField] float arenaMaxX = 35f;

    [Header("Flow")]
    [SerializeField] float startDelay = 2f;
    [SerializeField] float maxTurnWait = 2f;     // fighting with another boss: longest he waits for it to finish attacking
    [SerializeField] float restTime = 0.6f;
    [SerializeField, Range(0f, 1f)] float phase2At = 0.6f;
    [SerializeField] float phase2SpeedUp = 1.5f;

    [Header("Attacks Used")]
    [SerializeField] bool useCharge = true;
    [SerializeField] bool useSlam = false;
    [SerializeField] bool useRain = false;
    [SerializeField] bool useVomit = true;
    [SerializeField] bool useClaws = false;     // claws are off: touching him hurts instead

    [Header("Touch")]
    [SerializeField] bool touchHurts = true;    // ON: touching him hurts (contact damage)
    [SerializeField] bool touchPoisons = false; // ON: touching him also poisons
    [SerializeField] int touchDamage = 10;
    [SerializeField] float touchKnockback = 10f;

    [Header("1) Charge")]
    [SerializeField] float chargeWarnTime = 0.5f;
    [SerializeField] float chargeSpeed = 22f;
    [SerializeField] int chargeDamage = 14;      // (old touch-based charge damage, not used by the lizard dash)
    [SerializeField] int dashHitDamage = 22;     // lizard dash: hitting the player hurts a lot (no poison)
    [SerializeField] float dashKnockback = 16f;
    [SerializeField] float crashStunTime = 0.8f; // dodged: he hits the wall and is stunned this long
    [SerializeField] int crashPuddles = 10;       // dodged: poison puddles that appear all over the floor
    [SerializeField] int crashDrops = 14;        // dodged: poison drops that fall all over the arena
    [SerializeField] bool useLedgeDash = true;   // after the crash: climb onto the side ledge and dash across up there
    [SerializeField] float ledgeTopY = 3.5f;     // top of the two side ledges (the first level)
    [SerializeField] float climbTime = 0.45f;
    [SerializeField] float ledgeDashWarnTime = 0.35f;
    [SerializeField] float dropTime = 0.35f;

    [Header("2) Slam")]
    [SerializeField] float slamAirTime = 0.9f;
    [SerializeField] float slamHeight = 8f;
    [SerializeField] int slamDamage = 16;
    [SerializeField] float slamRadius = 3.5f;
    [SerializeField] float slamPuddleWidth = 7f;
    [SerializeField] float puddleLifetime = 5f;

    [Header("3) Rain")]
    [SerializeField] int rainDrops = 9;
    [SerializeField] float rainTime = 2.5f;

    [Header("4) Spit (old, replaced by Vomit)")]
    [SerializeField] Projectile spitPrefab;
    [SerializeField] int spitCount = 3;
    [SerializeField] float spitSpread = 18f; // degrees between globs

    [Header("4) Vomit")]
    [SerializeField] float vomitWarnTime = 0.35f;
    [SerializeField] int vomitGlobs = 9;
    [SerializeField] float vomitSpeed = 13f;
    [SerializeField] float vomitArc = 35f;       // degrees: globs spread between flat and this far up
    [SerializeField] int vomitDamage = 6;        // per glob, plus poison
    [SerializeField] float vomitGlobSize = 0.5f;
    [SerializeField] float vomitPuddleWidth = 2.5f;

    [Header("5) Claws (close range, not poisonous)")]
    [SerializeField] float clawTriggerDistance = 5f; // uses the claws when the player is this close (from his edge)
    [SerializeField] float clawApproachTime = 0.6f;  // steps in for up to this long before swiping
    [SerializeField] float clawStepSpeed = 6f;
    [SerializeField] float clawReach = 3f;           // how far in front of him a swipe hits
    [SerializeField] float clawHeight = 3.5f;
    [SerializeField] int clawSwipes = 2;
    [SerializeField] float clawWindup = 0.3f;        // flash before each swipe
    [SerializeField] float clawSwipeTime = 0.15f;    // how long each swipe can hit
    [SerializeField] int clawDamage = 7;
    [SerializeField] float clawKnockback = 10f;

    [Header("Masts (player camping on top)")]
    [SerializeField] float mastWalkSpeed = 9f;     // how fast he goes to the mast
    [SerializeField] float mastHitWindup = 0.25f;  // flash before each hit
    [SerializeField] float mastHitInterval = 0.35f;

    [Header("Phase 2 poison everywhere")]
    [SerializeField] float phase2PoisonInterval = 1f;
    [SerializeField] int phase2PuddlesPerWave = 3;
    [SerializeField] int phase2DropsPerWave = 3;

    [Header("Poison")]
    [SerializeField] int poisonDamage = 3;
    [SerializeField] float poisonDuration = 3f;

    [Header("On Death")]
    [SerializeField] GameObject healthBarRoot;  // hidden when he dies
    [SerializeField] GameObject[] removeOnDeath; // e.g. the exit gate
    [SerializeField] GameObject[] showOnDeath;   // e.g. the exit door

    [Header("Art (sprite frames; filled by Tools > Set Up Lizard Art)")]
    [SerializeField] SpriteRenderer artRenderer;   // child that shows the drawing (the body box stays for hits)
    [SerializeField] bool artFacesRight = true;    // the drawings face right
    [SerializeField] Sprite idleFrame;
    [SerializeField] Sprite[] dashFrames;          // wind-up, then spinning roll
    [SerializeField] int dashWindupFrames = 4;     // first frames: wind-up before the dash (played once)
    [SerializeField] float dashFps = 12f;
    [SerializeField] Sprite[] spewFrames;          // wind-up, then spewing
    [SerializeField] int spewWindupFrames = 5;
    [SerializeField] float spewFps = 12f;
    [SerializeField] Sprite[] poisonFrames;        // splash, puddle, drips
    [SerializeField] int puddleFrame = 1;          // the frame used for a puddle lying on the floor
    [SerializeField] float puddleArtHeight = 0.6f; // how tall a puddle drawing is
    [SerializeField] float splashFps = 14f;
    [SerializeField] bool splashOnPuddle = true;   // play the splash when a puddle appears

    [Header("Colors")]
    [SerializeField] Color bodyColor = new Color(0.25f, 0.65f, 0.15f);
    [SerializeField] Color warnColor = new Color(0.8f, 1f, 0.2f);
    [SerializeField] Color poisonColor = new Color(0.35f, 0.9f, 0.15f, 0.9f);

    Health health;
    PoisonHazard touchHazard;
    Rigidbody2D rb;
    BoxCollider2D body;
    SpriteRenderer sr;
    Transform player;
    Vector2 startPos;
    float halfWidth, halfHeight;
    bool phase2;
    int lastAttack = -1;
    readonly List<GameObject> spawned = new List<GameObject>();

    float Speed => phase2 ? phase2SpeedUp : 1f;

    void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        halfWidth = Mathf.Abs(transform.lossyScale.x) * body.size.x * 0.5f;
        halfHeight = Mathf.Abs(transform.lossyScale.y) * body.size.y * 0.5f;
        health.onDied.AddListener(OnDied);
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc)
        {
            player = pc.transform;
            // The player can walk through his body; damage comes from the touch hitbox below
            foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(c, body);
        }

        var touch = new GameObject("TouchHitbox");
        touch.transform.SetParent(transform, false);
        var tc = touch.AddComponent<BoxCollider2D>();
        tc.isTrigger = true;
        tc.size = body.size * 0.9f;
        tc.offset = body.offset;
        touchHazard = touch.AddComponent<PoisonHazard>();
        SetTouchDamage(touchDamage);

        if (sr) sr.color = bodyColor;
        PlayIdle();
        Face(DirToPlayer);
        startPos = rb.position;
        StartCoroutine(FightLoop());
    }

    void OnEnable()  => PlayerRespawn.Respawned += ResetFight;
    void OnDisable() { PlayerRespawn.Respawned -= ResetFight; BossTurns.End(this); }

    // ---------------- Sprite animation ----------------
    Sprite[] animFrames;
    float animFps, animTime;
    bool animLoop;
    bool HasArt => artRenderer && idleFrame;

    void Play(Sprite[] frames, float fps, bool loop)
    {
        if (!HasArt || frames == null || frames.Length == 0) return;
        animFrames = frames;
        animFps = Mathf.Max(fps, 0.01f);
        animLoop = loop;
        animTime = 0f;
        artRenderer.sprite = frames[0];
    }

    void PlayIdle()
    {
        if (!HasArt) return;
        animFrames = null;
        artRenderer.sprite = idleFrame;
    }

    static Sprite[] Part(Sprite[] frames, int first, int count)
    {
        if (frames == null || frames.Length == 0) return null;
        first = Mathf.Clamp(first, 0, frames.Length - 1);
        count = Mathf.Clamp(count, 1, frames.Length - first);
        var part = new Sprite[count];
        System.Array.Copy(frames, first, part, 0, count);
        return part;
    }

    void Face(float dir)
    {
        if (artRenderer && dir != 0f) artRenderer.flipX = artFacesRight ? dir < 0f : dir > 0f;
    }

    // Plays a wind-up (frames from first, count) once and waits for it; with no art it flashes instead
    IEnumerator Windup(Sprite[] frames, int count, float fps, float flashTime)
    {
        Sprite[] part = Part(frames, 0, count);
        if (HasArt && part != null)
        {
            Play(part, fps, false);
            yield return new WaitForSeconds(part.Length / Mathf.Max(fps, 0.01f) / Speed);
        }
        else yield return Warn(flashTime);
    }

    void Update()
    {
        if (HasArt && animFrames != null && animFrames.Length > 0)
        {
            animTime += Time.deltaTime;
            int i = Mathf.FloorToInt(animTime * animFps);
            artRenderer.sprite = animFrames[animLoop ? i % animFrames.Length : Mathf.Min(i, animFrames.Length - 1)];
        }

        if (!phase2 && health.CurrentHealth > 0 && health.CurrentHealth <= health.MaxHealth * phase2At)
        {
            phase2 = true;
            if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.5f, 0.3f);
            StartCoroutine(PoisonEverywhere());
        }
    }

    // ---------------- Fight loop ----------------

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(startDelay);
        while (true)
        {
            if (!player) { yield return null; continue; }

            // The player is camping on a mast: go break it first
            BreakableMast camped = null;
            foreach (BreakableMast m in BreakableMast.All) if (m.PlayerCamping) { camped = m; break; }
            if (camped)
            {
                yield return AttackMast(camped);
                Tint(bodyColor);
                yield return new WaitForSeconds(restTime / Speed);
                continue;
            }

            // Fighting together with another boss (Level 3): wait (up to Max Turn Wait) while it attacks
            for (float w = 0f; w < maxTurnWait && BossTurns.OtherIsAttacking(this); w += Time.deltaTime) yield return null;
            BossTurns.Begin(this);

            // Close: claws. Otherwise a random attack (never the same twice in a row)
            var options = new List<int>();
            if (useCharge) options.Add(0);
            if (useSlam) options.Add(1);
            if (useRain) options.Add(2);
            if (useVomit) options.Add(3);
            bool close = useClaws && Mathf.Abs(player.position.x - rb.position.x) - halfWidth <= clawTriggerDistance;
            if (options.Count > 1) options.Remove(lastAttack);
            int attack = close ? 4 : options.Count > 0 ? options[Random.Range(0, options.Count)] : 4;
            lastAttack = attack;

            switch (attack)
            {
                case 0: yield return Charge(); break;
                case 1: yield return Slam(); break;
                case 2: yield return Rain(); break;
                case 3: yield return Vomit(); break;
                default: yield return Claws(); break;
            }
            BossTurns.End(this);
            Tint(bodyColor);
            yield return new WaitForSeconds(restTime / Speed);
        }
    }

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
            if (trail && phase2 && Mathf.Abs(x - nextTrail) >= 2.5f) // phase 2: poison trail behind him
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

    // ---------------- Helpers ----------------

    float DirToPlayer => player && player.position.x < rb.position.x ? -1f : 1f;

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
        go.AddComponent<PoisonHazard>().Setup(0, 0f, poisonDamage, 2f, 0.5f, false);

        // With the poison art: hide the plain box, show the puddle drawing (and a splash when it appears)
        if (poisonFrames != null && poisonFrames.Length > 0)
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

    // ---------------- Reset and death ----------------

    // The player died: full health, back to the start, fight starts over
    void ResetFight()
    {
        BossTurns.End(this);
        if (!health || health.CurrentHealth <= 0) return;
        StopAllCoroutines();
        ClearSpawned();
        health.Heal(health.MaxHealth);
        phase2 = false;
        lastAttack = -1;
        SetTouchDamage(touchDamage);
        rb.position = startPos;
        Tint(bodyColor);
        foreach (BreakableMast m in BreakableMast.All) m.Restore();
        PlayIdle();
        if (healthBarRoot) healthBarRoot.SetActive(true);
        StartCoroutine(FightLoop());
    }

    void OnDied()
    {
        BossTurns.End(this);
        PlayerUpgrades.UnlockPoisonHook(); // the player puts his teeth on the hook: hook hits poison
        StopAllCoroutines();
        ClearSpawned();
        if (healthBarRoot) healthBarRoot.SetActive(false);
        foreach (GameObject go in removeOnDeath) if (go) go.SetActive(false);
        foreach (GameObject go in showOnDeath) if (go) go.SetActive(true);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.6f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.DrawLine(new Vector3(arenaMinX, ceilingY), new Vector3(arenaMaxX, ceilingY));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, groundY + 0.5f), slamRadius);
    }
}
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
public partial class PoisonBoss : MonoBehaviour, IChainStunnable
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
    [SerializeField] float phase2TrailGap = 2.5f; // phase 2: his dash leaves a puddle every this many tiles (0 = no trail)
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
    [SerializeField] int aimedVomitGlobs = 3;          // Level 3: when the Siren chains you, he vomits straight at you
    [SerializeField] float aimedVomitDelay = 0.3f;     // wind-up before those globs
    [SerializeField] float aimedVomitFlightTime = 0.8f; // how long they fly before landing on you
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
    [SerializeField] bool runsAwayWhenBeaten = false; // Level 2: he flees instead of dying (he is back in Level 3)
    [SerializeField, Range(0f, 1f)] float runAwayAt = 0.3f; // with runsAwayWhenBeaten: the fight ends when his health drops below this
    [SerializeField] float fleeSpeed = 50f;        // how fast he bolts out of the room
    [SerializeField] float fleeDelay = 0.15f;      // tiny pause before he bolts
    [SerializeField] string runAwayMessage = "Oscar runs away!";
    [SerializeField] GameObject[] removeOnDeath; // e.g. the exit gate
    [SerializeField] GameObject[] showOnDeath;   // e.g. the exit door

    [Header("Art (sprite frames; filled by Tools > Art > Oscar (Lizard))")]
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

    // Player's Siren's Chain: stunned, then slower for a while (see ChainStun)
    float slowUntil, slowFactor = 1f, resumeDelay = -1f;
    float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;
    PoisonHazard touchHazard;
    Rigidbody2D rb;
    BoxCollider2D body;
    SpriteRenderer sr;
    Transform player;
    Vector2 startPos;
    float halfWidth, halfHeight;
    bool phase2;
    bool fled;           // Level 2: already ran away
    int lastAttack = -1;
    readonly List<GameObject> spawned = new List<GameObject>();

    float Speed => (phase2 ? phase2SpeedUp : 1f) * SlowFactor;

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
        health.BeforeDeath = () =>
        {
            if (!runsAwayWhenBeaten) return false; // normal death
            Flee();
            return true;                           // he isn't destroyed
        };
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

        // Level 2: the fight ends early, he runs off before he is beaten all the way
        if (runsAwayWhenBeaten && health.CurrentHealth > 0 && health.CurrentHealth < health.MaxHealth * runAwayAt)
            Flee();
    }

    // ---------------- Fight loop ----------------

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(resumeDelay >= 0f ? resumeDelay : startDelay); // after a chain stun: only the stun
        resumeDelay = -1f;
        while (true)
        {
            while (DialogueManager.Busy) yield return null; // people are talking: no attacks
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

    // ---------------- Helpers ----------------

    float DirToPlayer => player && player.position.x < rb.position.x ? -1f : 1f;

    // ---------------- Reset and death ----------------

    // The player died: full health, back to the start, fight starts over

    // The player's Siren's Chain hit him: he stops what he is doing for stunTime, then is slower for slowTime
    public void ChainStun(float stunTime, float slowMultiplier, float slowTime)
    {
        if (!health || health.CurrentHealth <= 0) return;
        slowFactor = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
        slowUntil = Time.time + stunTime + slowTime;
        StopAllCoroutines();
        BossTurns.End(this);
        ClearSpawned();
        SetTouchDamage(touchDamage);
        rb.position = new Vector2(rb.position.x, startPos.y); // on a ledge: drops back to the floor
        Tint(bodyColor);
        PlayIdle();
        resumeDelay = stunTime;
        StartCoroutine(FightLoop());
    }

    void ResetFight()
    {
        BossTurns.End(this);
        if (!health || health.CurrentHealth <= 0 || fled) return;
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

    // Beaten in Level 2: gate opens, poison hook unlocks, bar hides, and he runs off (only once)
    void Flee()
    {
        if (fled) return;
        fled = true;
        OnDied();
        StartCoroutine(RunAway());
    }

    // Beaten in Level 2: he spins off out of the arena, away from the player, and is gone
    IEnumerator RunAway()
    {
        if (touchHazard && touchHazard.TryGetComponent(out Collider2D touchCol)) touchCol.enabled = false;
        if (TryGetComponent(out Collider2D bodyCol)) bodyCol.enabled = false; // doesn't block the player
        if (!string.IsNullOrEmpty(runAwayMessage)) PlayerItems.Toast(runAwayMessage);

        float dir = player && player.position.x > rb.position.x ? -1f : 1f; // away from the player
        Face(dir);
        yield return new WaitForSeconds(fleeDelay);
        Play(Part(dashFrames, dashWindupFrames, (dashFrames != null ? dashFrames.Length : 0) - dashWindupFrames), dashFps, true);
        float targetX = dir > 0f ? arenaMaxX + 30f : arenaMinX - 30f; // far past the edge of the screen
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        while (Mathf.Abs(rb.position.x - targetX) > 0.1f)
        {
            rb.MovePosition(new Vector2(Mathf.MoveTowards(rb.position.x, targetX, fleeSpeed * Time.deltaTime), startPos.y));
            // Fades out as he leaves the arena
            float outside = dir > 0f ? rb.position.x - arenaMaxX : arenaMinX - rb.position.x;
            float a = Mathf.Clamp01(1f - (outside - 15f) / 10f); // only fades once he is well off screen
            foreach (SpriteRenderer r in renderers) if (r && r.enabled) { Color c = r.color; c.a = a; r.color = c; }
            yield return null;
        }
        gameObject.SetActive(false);
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
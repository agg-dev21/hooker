using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// HARDY: the Level 1 boss (Venom-style). Attacks in random order (never the same one twice in a row):
// 1) Tentacle Lash: a tentacle shoots along the ground toward the player. Body = less damage, tip = more.
// 2) Jump Slam: jumps high, spins in the air and slams down near the player (shockwave AOE).
//    Optional: Roll After Landing makes him also roll along the ground at the player.
// 3) Spikes: spikes burst out of the ground one after another toward the player.
// 4) Punch: only when the player is close, his goo arm becomes a blade and swings in front of him.
// Phase 2 (at half health): spikes also keep erupting at random spots all over the deck.
// No warning flash: before each attack the boss strikes a pose. Attacks with art play their sprite frames;
// attacks without art yet use a placeholder (the box changes shape). Between attacks he walks toward the player.
// On death he crumbles into pieces. If the player dies, he gets all his health back and the fight restarts.
// Every value is in the Inspector. Touching him does no damage by default (Touch Hurts), only his attacks do.
// Needs: SpriteRenderer, BoxCollider2D (solid), Rigidbody2D (Kinematic), Health.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public partial class HardyBoss : MonoBehaviour, IChainStunnable
{
    [Header("Arena")]
    [SerializeField] float groundY = -5f;     // top surface of the deck
    [SerializeField] float arenaMinX = -13f;  // left end of the deck
    [SerializeField] float arenaMaxX = 66f;   // right end of the deck

    [Header("Fight Flow")]
    [SerializeField] float startDelay = 1.5f; // delay before the first attack
    [SerializeField] float restTime = 0.5f;   // pause between attacks
    [SerializeField] GameObject healthBarRoot; // hidden when the boss dies
    [SerializeField] string sceneAfterDeath;   // empty = nothing, filled = ask to continue, then fade to this scene
    [SerializeField] float sceneAfterDeathDelay = 2f;   // seconds after he dies (and his last words) before the player is asked to continue
    [SerializeField] string continueMessage = "Hardy is defeated.\nSet sail?";
    [SerializeField] Animator animator;        // optional: pose triggers are sent here if you use an Animator

    [Header("Art (sprite frames; filled by Tools > Art > Hardy)")]
    [SerializeField] bool artFacesRight = false;   // the drawings face left
    [SerializeField] Sprite blockSprite;           // plain square for attacks that have no art yet
    [SerializeField] Sprite[] walkFrames;          // first frame is also used when standing still
    [SerializeField] float walkFps = 8f;

    [Header("Touch")]
    [SerializeField] bool blocksPlayer = false;    // OFF: the player can jump over / pass through him
    [SerializeField] bool touchHurts = false;      // OFF: touching him does no damage, only his attacks hurt
    [SerializeField] int touchDamage = 10;         // only used when Touch Hurts is ON (no fire)
    [SerializeField] float touchKnockback = 8f;

    [Header("Walking (between attacks)")]
    [SerializeField] bool walkBetweenAttacks = true;
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float walkStopDistance = 6f;  // stops walking when this close to the player

    [Header("Fire (used by attacks that burn)")]
    [SerializeField] int burnDamage = 2;
    [SerializeField] float burnTickInterval = 1f;
    [SerializeField] float burnDuration = 3f;

    [Header("1) Tentacle Lash")]
    [SerializeField] bool useTentacle = true;
    [SerializeField] float tentaclePoseTime = 0.1f;            // how long the pose lasts before the attack
    [SerializeField] Vector2 tentaclePoseScale = new Vector2(1.3f, 0.85f); // pose: a bit wider and lower
    [SerializeField] string tentacleAnimTrigger = "Tentacle";
    [SerializeField] float tentacleLength = 18f;
    [SerializeField] float tentacleTipLength = 2f;
    [SerializeField] float tentacleThickness = 0.8f;
    [SerializeField] float tentacleHeight = 1f;       // height above the ground
    [SerializeField] float tentacleExtendSpeed = 70f;
    [SerializeField] float tentacleHoldTime = 0.3f;
    [SerializeField] float tentacleRetractSpeed = 30f;
    [SerializeField] int tentacleBodyDamage = 6;
    [SerializeField] int tentacleTipDamage = 10;
    [SerializeField] float tentacleKnockback = 10f;
    [SerializeField] bool tentacleBurns = true;
    [SerializeField] Color tentacleColor = new Color(0.08f, 0.05f, 0.12f);
    [SerializeField] Color tentacleTipColor = new Color(0.15f, 0.08f, 0.2f);
    [SerializeField] Sprite[] tentaclePoseFrames;     // if set, this plays instead of the box pose (last frame is held during the attack)
    [SerializeField] float tentaclePoseFps = 14f;
    [SerializeField] Sprite[] tentacleFxFrames;       // if set, the tentacle plays this animation (drawn growing to the left)
    [SerializeField] float tentacleFxFps = 18f;
    [SerializeField] int tentacleHitFirstFrame = 6;   // frames (counting from 0) during which the tentacle can hurt
    [SerializeField] int tentacleHitLastFrame = 11;
    [SerializeField] float tentacleFxThicknessScale = 1f; // vertical size of the drawing (length follows Tentacle Length)

    [Header("2) Leap + Roll")]
    [SerializeField] bool useLeap = true;
    [SerializeField] float leapPoseTime = 0.1f;
    [SerializeField] Vector2 leapPoseScale = new Vector2(1.2f, 0.7f); // pose: crouches down
    [SerializeField] string leapAnimTrigger = "Leap";
    [SerializeField] float leapHeight = 7f;
    [SerializeField] float leapSpeed = 22f;          // sideways speed through the air: longer jumps take longer (and spin more)
    [SerializeField] float leapMinTime = 0.6f;       // even a short jump lasts at least this long
    [SerializeField] float leapMaxTime = 2.5f;       // even a very long jump lasts at most this long
    [SerializeField] float leapLandOffset = 0f;       // 0 = lands right where the player was when he jumped
    [SerializeField] float landingRadius = 7f;
    [SerializeField] int landingDamage = 12;
    [SerializeField] float landingKnockback = 12f;
    [SerializeField] bool landingBurns = true;
    [SerializeField] Color shockwaveColor = new Color(1f, 0.45f, 0.1f, 0.5f);
    [SerializeField] float shockwaveShowTime = 0.2f;
    [SerializeField] bool rollAfterLanding = false;   // ON: after the slam he also rolls along the ground at the player
    [SerializeField] float rollPoseTime = 0.1f;
    [SerializeField] Vector2 rollPoseScale = new Vector2(0.8f, 0.8f); // pose: curls into a ball
    [SerializeField] string rollAnimTrigger = "Roll";
    [SerializeField] float rollSpeed = 14f;
    [SerializeField] float rollDistance = 12f;
    [SerializeField] int rollDamage = 10;
    [SerializeField] float rollKnockback = 12f;
    [SerializeField] bool rollBurns = true;
    [SerializeField] Sprite[] jumpSlamFrames;         // if set: wind-up, then spin frames loop for the whole jump and roll, then landing
    [SerializeField] float jumpSlamFps = 12f;
    [SerializeField] int jumpWindupFrames = 4;        // first frames: wind-up before the jump (played once)
    [SerializeField] int jumpSpinFirstFrame = 4;      // spin frames (counting from 0) that loop while in the air and rolling
    [SerializeField] int jumpSpinFrameCount = 3;
    [SerializeField] int jumpLandFrame = 7;           // shown when he lands and when the roll ends
    [SerializeField] float jumpLandHoldTime = 0.25f;  // how long the landing frame stays

    [Header("3) Spikes")]
    [SerializeField] bool useSpikes = true;
    [SerializeField] float spikesPoseTime = 0.1f;
    [SerializeField] Vector2 spikesPoseScale = new Vector2(0.85f, 1.25f); // pose: stands up tall
    [SerializeField] string spikesAnimTrigger = "Spikes";
    [SerializeField] int spikeCount = 8;
    [SerializeField] float spikeStartDistance = 2f;   // distance from the boss to the first spike
    [SerializeField] float spikeSpacing = 2.2f;
    [SerializeField] float spikeInterval = 0.1f;     // time between one spike and the next
    [SerializeField] float spikeWidth = 1f;
    [SerializeField] float spikeHeight = 3f;
    [SerializeField] float spikeRiseSpeed = 30f;
    [SerializeField] float spikeUpTime = 0.4f;
    [SerializeField] int spikeDamage = 8;
    [SerializeField] float spikeKnockback = 10f;
    [SerializeField] bool spikesBurn = true;
    [SerializeField] Color spikeColor = new Color(0.1f, 0.05f, 0.15f);   // only used when there is no spike art
    [SerializeField] Sprite[] spikesPoseFrames;        // if set, this plays instead of the box pose
    [SerializeField] float spikesPoseFps = 14f;
    [SerializeField] int spikesPoseLoopFrames = 4;     // the last frames of the pose keep looping while spikes erupt
    [SerializeField] Sprite[] spikeFxFrames;           // if set, each spike plays this animation
    [SerializeField] float spikeFxFps = 16f;
    [SerializeField] int spikeHitFirstFrame = 4;       // frames (counting from 0) during which the spike can hurt
    [SerializeField] int spikeHitLastFrame = 10;
    [SerializeField] float spikeFxScale = 1f;

    [Header("4) Punch (close range)")]
    [SerializeField] bool usePunch = true;
    [SerializeField] float punchTriggerDistance = 7f;  // only used when the player is at most this far away
    [SerializeField] Sprite[] punchFrames;
    [SerializeField] float punchFps = 12f;
    [SerializeField] int punchHitFirstFrame = 3;       // frames (counting from 0) during which the blade can hurt
    [SerializeField] int punchHitLastFrame = 3;
    [SerializeField] float punchRange = 3.5f;          // how far in front of him the blade reaches
    [SerializeField] float punchHitHeight = 4f;        // how tall the hit area is (from the ground up)
    [SerializeField] int punchDamage = 8;
    [SerializeField] float punchKnockback = 12f;
    [SerializeField] bool punchBurns = false;
    [SerializeField] float punchPlaceholderTime = 0.4f; // without art: how long the swing lasts
    [SerializeField] float punchRestTime = 0.2f;        // pause after a punch while the player is still close (spam punch)
    [SerializeField, Range(0f, 1f)] float punchAlwaysAboveHealth = 0.3f; // above this part of his max health, a close player ALWAYS gets punched
    [SerializeField, Range(0f, 1f)] float punchPreferChance = 0.5f; // below it: chance he still picks the punch when the player is close
    [SerializeField] float punchApproachTime = 0.8f;  // before punching: walk toward the player for up to this long until in reach
    [SerializeField] int maxPunchCombo = 3;           // punches in a row before he must use a different attack

    [Header("Phase 2 (random spikes)")]
    [SerializeField] bool usePhase2 = true;
    [SerializeField, Range(0f, 1f)] float phase2AtHealth = 0.5f; // starts when his health drops to this part of the max (0.5 = half)
    [SerializeField] float randomSpikeInterval = 0.9f;  // seconds between random spike waves
    [SerializeField] int randomSpikesPerWave = 3;
    [SerializeField] float randomSpikeMinGap = 3f;      // spikes in one wave are at least this far apart
    [SerializeField] float phase2SpeedMultiplier = 1.3f; // in phase 2 he walks this much faster
    [SerializeField] float phase2RestMultiplier = 0.6f;  // in phase 2 his rest between attacks is this much shorter

    [Header("Death (placeholder: crumbles)")]
    [SerializeField] int crumblePieces = 16;
    [SerializeField] float crumbleForce = 6f;
    [SerializeField] float crumbleLifetime = 1.5f;
    [SerializeField] float crumbleGravity = 2f;
    [SerializeField] Color crumbleColor = new Color(0.12f, 0.08f, 0.18f);

    Rigidbody2D rb;
    BoxCollider2D body;
    SpriteRenderer sr;
    Health health;

    // Player's Siren's Chain: stunned, then slower for a while (see ChainStun)
    float slowUntil, slowFactor = 1f, resumeDelay = -1f;
    float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;
    Transform player;
    Vector3 baseScale;
    Vector2 baseHalfSize;
    Vector2 pose = Vector2.one;
    bool movingSelf; // leaping or rolling: the boss is moving itself
    float walkDir;   // -1, 0 or 1 while walking between attacks
    Sprite[] animFrames;
    float animFps;
    bool animLoop;
    float animTime;
    bool HasArt => walkFrames != null && walkFrames.Length > 0;
    bool phase2;
    int lastAttack = -1;
    int punchCombo;          // punches in a row
    bool lastPunchSwung;     // false when the punch was cancelled because the player got out of reach
    readonly List<GameObject> spawned = new List<GameObject>();
    Vector2 startPosition;   // where the fight started (he goes back here when the player dies)
    int lastResetFrame = -1; // so two death signals in the same frame only reset once

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        body = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        baseScale = transform.localScale;
        baseHalfSize = new Vector2(Mathf.Abs(transform.lossyScale.x) * body.size.x, transform.lossyScale.y * body.size.y) * 0.5f;
        health.onDied.AddListener(OnDied);
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc) player = pc.transform;

        // Touch damage now comes only from the optional TouchHitbox below, never from ContactDamage
        if (TryGetComponent(out ContactDamage contact)) contact.enabled = false;

        // The body does not push the player, so the player can jump over or pass through him
        if (!blocksPlayer && pc)
            foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(c, body);

        if (touchHurts)
        {
            var touch = new GameObject("TouchHitbox");
            touch.transform.SetParent(transform, false);
            var touchCol = touch.AddComponent<BoxCollider2D>();
            touchCol.isTrigger = true;
            touchCol.size = body.size;
            touchCol.offset = body.offset;
            touch.AddComponent<DamageZone>().Setup(touchDamage, touchKnockback, false, 0, 0f, 1f);
        }

        // Second way to notice the player dying (backup for the PlayerRespawn event below)
        if (pc && pc.TryGetComponent(out Health playerHealth)) playerHealth.onDied.AddListener(ResetFight);

        startPosition = rb.position;
        StartCoroutine(FightLoop());
    }

    void OnEnable()  => PlayerRespawn.Respawned += ResetFight;
    void OnDisable() => PlayerRespawn.Respawned -= ResetFight;

    // The player died (or fell off): the boss gets all his health back and the fight starts over
    void ResetFight()
    {
        if (!health || health.CurrentHealth <= 0) return; // already beaten: nothing to reset
        if (Time.frameCount == lastResetFrame) return;   // already reset this frame
        lastResetFrame = Time.frameCount;
        Debug.Log("Hardy: player died, boss health reset to " + health.MaxHealth, this);

        StopAllCoroutines(); // stops the current attack, the fight loop and phase 2 spikes

        // Remove every hitbox, tentacle, spike and shockwave still out
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();

        // Back to the start: full health, phase 1, standing where the fight began
        health.Heal(health.MaxHealth);
        phase2 = false;
        movingSelf = false;
        walkDir = 0f;
        lastAttack = -1;
        punchCombo = 0;
        lastPunchSwung = false;
        transform.rotation = Quaternion.identity;
        SetPose(Vector2.one);
        rb.position = new Vector2(ClampX(startPosition.x), GroundCenterY);
        if (healthBarRoot) healthBarRoot.SetActive(true);
        PlayIdle();

        StartCoroutine(FightLoop()); // waits Start Delay before the first attack, like the first time
    }

    // The player's Siren's Chain hit him: he stops what he is doing for stunTime, then is slower for slowTime
    public void ChainStun(float stunTime, float slowMultiplier, float slowTime)
    {
        if (!health || health.CurrentHealth <= 0) return;
        slowFactor = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
        slowUntil = Time.time + stunTime + slowTime;
        StopAllCoroutines();
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
        movingSelf = false;
        walkDir = 0f;
        transform.rotation = Quaternion.identity;
        SetPose(Vector2.one);
        rb.position = new Vector2(rb.position.x, GroundCenterY); // mid-jump: drops back to the deck
        PlayIdle();
        if (phase2) StartCoroutine(RandomSpikes());
        resumeDelay = stunTime;
        StartCoroutine(FightLoop());
    }

    void FixedUpdate()
    {
        // Stay planted on the ground so punches cannot knock it into the air (except while leaping/rolling).
        // While walking, also step toward the player.
        float speed = (phase2 ? walkSpeed * phase2SpeedMultiplier : walkSpeed) * SlowFactor;
        if (!movingSelf) rb.MovePosition(new Vector2(ClampX(rb.position.x + walkDir * speed * Time.fixedDeltaTime), GroundCenterY));
    }

    // ---------------- Sprite animation ----------------
    void Update()
    {
        if (usePhase2 && !phase2 && health.CurrentHealth > 0 && health.CurrentHealth <= health.MaxHealth * phase2AtHealth)
        {
            phase2 = true;
            StartCoroutine(RandomSpikes());
        }

        if (!sr || animFrames == null || animFrames.Length == 0) return;
        animTime += Time.deltaTime;
        int i = Mathf.FloorToInt(animTime * animFps);
        i = animLoop ? i % animFrames.Length : Mathf.Min(i, animFrames.Length - 1);
        sr.sprite = animFrames[i];
    }

    // Plays a set of frames (does nothing if it is already playing them)
    void Play(Sprite[] frames, float fps, bool loop)
    {
        if (frames == null || frames.Length == 0) return;
        if (frames == animFrames && loop == animLoop) return;
        animFrames = frames;
        animFps = Mathf.Max(fps, 0.01f);
        animLoop = loop;
        animTime = 0f;
    }

    Sprite[] idleFrames;
    void PlayIdle()
    {
        if (!HasArt) return;
        if (idleFrames == null) idleFrames = new[] { walkFrames[0] };
        Play(idleFrames, 1f, true);
    }

    // Walks toward the player for this long (stops when close enough), then stands still
    IEnumerator WalkPhase(float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float dist = player ? Mathf.Abs(player.position.x - rb.position.x) : 0f;
            bool walk = walkBetweenAttacks && player && dist > walkStopDistance;
            walkDir = walk ? DirToPlayer : 0f;
            if (walk) { Face(walkDir); Play(walkFrames, walkFps, true); }
            else PlayIdle();
            yield return null;
        }
        walkDir = 0f;
        PlayIdle();
    }

    Vector2 HalfSize => new Vector2(baseHalfSize.x * pose.x, baseHalfSize.y * pose.y);
    float GroundCenterY => groundY + HalfSize.y;
    float ClampX(float x) => Mathf.Clamp(x, arenaMinX + HalfSize.x, arenaMaxX - HalfSize.x);
    float DirToPlayer => player ? Mathf.Sign(player.position.x - rb.position.x) : -1f;

    IEnumerator FightLoop()
    {
        PlayIdle();
        yield return new WaitForSeconds(resumeDelay >= 0f ? resumeDelay : startDelay); // after a chain stun: only the stun
        resumeDelay = -1f;

        while (true)
        {
            while (DialogueManager.Busy) yield return null; // people are talking: no attacks
            var options = new List<int>();
            if (useTentacle) options.Add(0);
            if (useLeap) options.Add(1);
            if (useSpikes) options.Add(2);
            bool playerClose = usePunch && player && Mathf.Abs(player.position.x - rb.position.x) <= punchTriggerDistance;
            bool healthy = health.CurrentHealth > health.MaxHealth * punchAlwaysAboveHealth;
            // Too many punches in a row: he has to do something else first (stops endless punch loops)
            bool punchAllowed = playerClose && punchCombo < maxPunchCombo;
            if (punchAllowed) options.Add(3);
            if (options.Count > 1 && !(punchAllowed && healthy)) options.Remove(lastAttack); // never the same attack twice in a row
            if (options.Count == 0 || !player) { yield return null; continue; }

            // Close and still healthy: always punch. Otherwise punch sometimes, or any attack at random.
            int attack = punchAllowed && healthy ? 3
                : options.Contains(3) && Random.value < punchPreferChance ? 3
                : options[Random.Range(0, options.Count)];
            lastAttack = attack;
            punchCombo = attack == 3 ? punchCombo + 1 : 0;

            if (attack == 0) yield return TentacleLash();
            else if (attack == 1) yield return LeapAndRoll();
            else if (attack == 2) yield return Spikes();
            else yield return Punch();

            // Player still inside the blade's reach after a real punch: punch again almost right away.
            // Otherwise rest (and walk) as normal.
            bool stillInReach = player && Mathf.Abs(player.position.x - rb.position.x) <= PunchReach;
            if (attack == 3 && lastPunchSwung && stillInReach) yield return new WaitForSeconds(punchRestTime);
            else yield return WalkPhase((phase2 ? restTime * phase2RestMultiplier : restTime) / SlowFactor);
        }
    }

    // ---------------- Pose (before an attack) ----------------
    IEnumerator Pose(Vector2 scale, float time, string animTrigger)
    {
        if (animator && !string.IsNullOrEmpty(animTrigger)) animator.SetTrigger(animTrigger);
        SetPose(scale);
        yield return new WaitForSeconds(time);
    }

    void SetPose(Vector2 scale)
    {
        if (HasArt) return; // real art: do not squash the drawing
        pose = scale;
        transform.localScale = new Vector3(baseScale.x * scale.x, baseScale.y * scale.y, baseScale.z);
        if (!movingSelf) rb.position = new Vector2(ClampX(rb.position.x), GroundCenterY); // keep feet on the ground
    }

    // ---------------- Death: crumbles (placeholder) ----------------
    void OnDied()
    {
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
        if (healthBarRoot) healthBarRoot.SetActive(false);

        // Pieces: small random boxes inside the boss's size that fly apart, fall and disappear
        Vector2 size = HalfSize * 2f;
        for (int i = 0; i < crumblePieces; i++)
        {
            var piece = new GameObject("HardyPiece");
            piece.transform.position = (Vector2)transform.position + new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
            float s = Random.Range(0.3f, 0.7f);
            piece.transform.localScale = new Vector3(s, s, 1f);
            var psr = piece.AddComponent<SpriteRenderer>();
            psr.sprite = blockSprite ? blockSprite : (sr ? sr.sprite : null);
            psr.color = crumbleColor;
            psr.sortingOrder = 3;
            var prb = piece.AddComponent<Rigidbody2D>();
            prb.gravityScale = crumbleGravity;
            prb.linearVelocity = new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)) * crumbleForce;
            prb.angularVelocity = Random.Range(-360f, 360f);
            Destroy(piece, crumbleLifetime);
        }

        PlayerAbility.UnlockHardySpikes(); // the player gets Hardy's sideways spike attack
        if (!string.IsNullOrEmpty(sceneAfterDeath)) LevelEndPrompt.Show(sceneAfterDeath, sceneAfterDeathDelay, continueMessage);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, landingRadius);

        // Red box = punch hit area (to the left; it flips toward the player in game; exact size shows in Play mode)
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(transform.position.x - PunchReach * 0.5f, groundY + punchHitHeight * 0.5f),
                            new Vector3(PunchReach, punchHitHeight));
    }
}
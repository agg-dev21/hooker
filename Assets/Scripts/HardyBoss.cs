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
// On death he crumbles into pieces.
// Every value is in the Inspector. Touching him does no damage by default (Touch Hurts), only his attacks do.
// Needs: SpriteRenderer, BoxCollider2D (solid), Rigidbody2D (Kinematic), Health.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public class HardyBoss : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] float groundY = -5f;     // top surface of the deck
    [SerializeField] float arenaMinX = -13f;  // left end of the deck
    [SerializeField] float arenaMaxX = 66f;   // right end of the deck

    [Header("Fight Flow")]
    [SerializeField] float startDelay = 1.5f; // delay before the first attack
    [SerializeField] float restTime = 0.5f;   // pause between attacks
    [SerializeField] GameObject healthBarRoot; // hidden when the boss dies
    [SerializeField] string sceneAfterDeath;   // empty = nothing, filled = fade to this scene
    [SerializeField] float sceneAfterDeathDelay = 2f; // seconds to watch him crumble before the fade starts
    [SerializeField] Animator animator;        // optional: pose triggers are sent here if you use an Animator

    [Header("Art (sprite frames; filled by Tools > Set Up Hardy Art)")]
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
    readonly List<GameObject> spawned = new List<GameObject>();

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
        PlayerController2D pc = FindFirstObjectByType<PlayerController2D>();
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

        StartCoroutine(FightLoop());
    }

    void FixedUpdate()
    {
        // Stay planted on the ground so punches cannot knock it into the air (except while leaping/rolling).
        // While walking, also step toward the player.
        float speed = phase2 ? walkSpeed * phase2SpeedMultiplier : walkSpeed;
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
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            var options = new List<int>();
            if (useTentacle) options.Add(0);
            if (useLeap) options.Add(1);
            if (useSpikes) options.Add(2);
            bool playerClose = usePunch && player && Mathf.Abs(player.position.x - rb.position.x) <= punchTriggerDistance;
            bool healthy = health.CurrentHealth > health.MaxHealth * punchAlwaysAboveHealth;
            if (playerClose) options.Add(3);
            if (options.Count > 1 && !(playerClose && healthy)) options.Remove(lastAttack); // never the same attack twice in a row
            if (options.Count == 0 || !player) { yield return null; continue; }

            // Close and still healthy: always punch. Otherwise punch sometimes, or any attack at random.
            int attack = playerClose && healthy ? 3
                : options.Contains(3) && Random.value < punchPreferChance ? 3
                : options[Random.Range(0, options.Count)];
            lastAttack = attack;

            if (attack == 0) yield return TentacleLash();
            else if (attack == 1) yield return LeapAndRoll();
            else if (attack == 2) yield return Spikes();
            else yield return Punch();

            // Player still close after a punch: punch again almost right away. Otherwise rest (and walk) as normal.
            bool stillClose = player && Mathf.Abs(player.position.x - rb.position.x) <= punchTriggerDistance;
            if (attack == 3 && stillClose) yield return new WaitForSeconds(punchRestTime);
            else yield return WalkPhase(phase2 ? restTime * phase2RestMultiplier : restTime);
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
        GameObject wave = MakePiece("Shockwave", shockwaveColor, false);
        wave.transform.position = new Vector3(to.x, groundY, 0f);
        wave.transform.localScale = new Vector3(landingRadius * 2f, landingRadius, 1f);
        StartCoroutine(RemoveAfter(wave, shockwaveShowTime));

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
    // The hit area is a box in front of him: Punch Range wide, Punch Hit Height tall, standing on the ground.
    IEnumerator Punch()
    {
        float dir = DirToPlayer;
        Face(dir);

        GameObject hit = MakePiece("PunchHitbox", Color.clear, true);
        var zone = hit.GetComponent<DamageZone>();
        zone.Setup(punchDamage, punchKnockback, punchBurns, burnDamage, burnDuration, burnTickInterval);
        float centerX = rb.position.x + dir * (HalfSize.x + punchRange * 0.5f);
        SetBox(hit, new Vector2(centerX, groundY + punchHitHeight * 0.5f), new Vector2(punchRange, punchHitHeight));

        if (punchFrames != null && punchFrames.Length > 0)
        {
            Play(punchFrames, punchFps, false);
            float fps = Mathf.Max(punchFps, 0.01f);
            for (int i = 0; i < punchFrames.Length; i++)
            {
                zone.Active = i >= punchHitFirstFrame && i <= punchHitLastFrame;
                yield return new WaitForSeconds(1f / fps);
            }
        }
        else
        {
            // No art yet: the hit area is simply active for a moment
            zone.Active = true;
            yield return new WaitForSeconds(punchPlaceholderTime);
        }

        Remove(hit);
        PlayIdle();
    }

    // ---------------- Helpers ----------------
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

        if (!string.IsNullOrEmpty(sceneAfterDeath)) ScreenFade.FadeToScene(sceneAfterDeath, 0.6f, 0.6f, sceneAfterDeathDelay);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, landingRadius);
    }
}

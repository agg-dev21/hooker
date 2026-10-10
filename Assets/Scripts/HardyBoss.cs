using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// HARDY: the Level 1 boss (Venom-style). Three attacks in random order (never the same one twice in a row):
// 1) Tentacle Lash: a tentacle shoots along the ground toward the player. Body = less damage, tip = more.
// 2) Leap + Roll: jumps high and lands near the player (shockwave AOE), then rolls at the player.
// 3) Spikes: spikes burst out of the ground one after another toward the player.
// No warning flash: before each attack the boss strikes a pose. Attacks with art play their sprite frames;
// attacks without art yet use a placeholder (the box changes shape). Between attacks he walks toward the player.
// On death he crumbles into pieces.
// Every value is in the Inspector. Touch damage is the separate ContactDamage script and does not burn.
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
    [SerializeField] float restTime = 1.2f;   // pause between attacks
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
    [SerializeField] bool blocksPlayer = false;    // OFF: the player can jump over / pass through him (touching still hurts)
    [SerializeField] int touchDamage = 10;         // no fire
    [SerializeField] float touchKnockback = 8f;

    [Header("Walking (between attacks)")]
    [SerializeField] bool walkBetweenAttacks = true;
    [SerializeField] float walkSpeed = 2.5f;
    [SerializeField] float walkStopDistance = 6f;  // stops walking when this close to the player

    [Header("Fire (used by attacks that burn)")]
    [SerializeField] int burnDamage = 5;
    [SerializeField] float burnTickInterval = 1f;
    [SerializeField] float burnDuration = 3f;

    [Header("1) Tentacle Lash")]
    [SerializeField] bool useTentacle = true;
    [SerializeField] float tentaclePoseTime = 0.1f;            // how long the pose lasts before the attack
    [SerializeField] Vector2 tentaclePoseScale = new Vector2(1.3f, 0.85f); // pose: a bit wider and lower
    [SerializeField] string tentacleAnimTrigger = "Tentacle";
    [SerializeField] float tentacleLength = 14f;
    [SerializeField] float tentacleTipLength = 2f;
    [SerializeField] float tentacleThickness = 0.8f;
    [SerializeField] float tentacleHeight = 1f;       // height above the ground
    [SerializeField] float tentacleExtendSpeed = 50f;
    [SerializeField] float tentacleHoldTime = 0.3f;
    [SerializeField] float tentacleRetractSpeed = 30f;
    [SerializeField] int tentacleBodyDamage = 10;
    [SerializeField] int tentacleTipDamage = 20;
    [SerializeField] float tentacleKnockback = 10f;
    [SerializeField] bool tentacleBurns = true;
    [SerializeField] Color tentacleColor = new Color(0.08f, 0.05f, 0.12f);
    [SerializeField] Color tentacleTipColor = new Color(0.15f, 0.08f, 0.2f);
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
    [SerializeField] float leapDuration = 1f;
    [SerializeField] float leapLandOffset = 3f;       // how far from the player it lands
    [SerializeField] float landingRadius = 3.5f;
    [SerializeField] int landingDamage = 20;
    [SerializeField] float landingKnockback = 12f;
    [SerializeField] bool landingBurns = true;
    [SerializeField] Color shockwaveColor = new Color(1f, 0.45f, 0.1f, 0.5f);
    [SerializeField] float shockwaveShowTime = 0.2f;
    [SerializeField] float rollPoseTime = 0.1f;
    [SerializeField] Vector2 rollPoseScale = new Vector2(0.8f, 0.8f); // pose: curls into a ball
    [SerializeField] string rollAnimTrigger = "Roll";
    [SerializeField] float rollSpeed = 14f;
    [SerializeField] float rollDistance = 12f;
    [SerializeField] int rollDamage = 20;
    [SerializeField] float rollKnockback = 12f;
    [SerializeField] bool rollBurns = true;

    [Header("3) Spikes")]
    [SerializeField] bool useSpikes = true;
    [SerializeField] float spikesPoseTime = 0.1f;
    [SerializeField] Vector2 spikesPoseScale = new Vector2(0.85f, 1.25f); // pose: stands up tall
    [SerializeField] string spikesAnimTrigger = "Spikes";
    [SerializeField] int spikeCount = 6;
    [SerializeField] float spikeStartDistance = 2f;   // distance from the boss to the first spike
    [SerializeField] float spikeSpacing = 2.2f;
    [SerializeField] float spikeInterval = 0.15f;     // time between one spike and the next
    [SerializeField] float spikeWidth = 1f;
    [SerializeField] float spikeHeight = 3f;
    [SerializeField] float spikeRiseSpeed = 30f;
    [SerializeField] float spikeUpTime = 0.4f;
    [SerializeField] int spikeDamage = 20;
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

        if (!blocksPlayer && pc)
        {
            // The body no longer pushes the player; a trigger hitbox the same size deals touch damage instead
            foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(c, body);
            if (TryGetComponent(out ContactDamage contact)) contact.enabled = false;
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
        if (!movingSelf) rb.MovePosition(new Vector2(ClampX(rb.position.x + walkDir * walkSpeed * Time.fixedDeltaTime), GroundCenterY));
    }

    // ---------------- Sprite animation ----------------
    void Update()
    {
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
            if (options.Count > 1) options.Remove(lastAttack); // never the same attack twice in a row
            if (options.Count == 0 || !player) { yield return null; continue; }

            int attack = options[Random.Range(0, options.Count)];
            lastAttack = attack;

            if (attack == 0) yield return TentacleLash();
            else if (attack == 1) yield return LeapAndRoll();
            else yield return Spikes();

            yield return WalkPhase(restTime);
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
        yield return Pose(tentaclePoseScale, tentaclePoseTime, tentacleAnimTrigger);

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
    IEnumerator LeapAndRoll()
    {
        Face(DirToPlayer);
        yield return Pose(leapPoseScale, leapPoseTime, leapAnimTrigger);
        SetPose(Vector2.one);

        // Land a little short of the player, inside the arena
        float targetX = ClampX(player.position.x - DirToPlayer * leapLandOffset);
        Vector2 from = rb.position;
        Vector2 to = new Vector2(targetX, GroundCenterY);

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

        // Roll toward the player
        float dir = DirToPlayer;
        Face(dir);
        yield return Pose(rollPoseScale, rollPoseTime, rollAnimTrigger);

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
                hit.attachedRigidbody.linearVelocity = new Vector2(dir, 0.6f).normalized * knockback;
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// HARDY: the Level 1 boss (Venom-style). Three attacks in random order (never the same one twice in a row):
// 1) Tentacle Lash: a tentacle shoots along the ground toward the player. Body = less damage, tip = more.
// 2) Leap + Roll: jumps high and lands near the player (shockwave AOE), then rolls at the player.
// 3) Spikes: spikes burst out of the ground one after another toward the player.
// No warning flash: before each attack the boss strikes a short pose (for now the box changes shape;
// once there is art, it can fire Animator triggers). On death it crumbles into pieces.
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
    [SerializeField] Animator animator;        // optional: pose triggers are sent here once there is art

    [Header("Fire (used by attacks that burn)")]
    [SerializeField] int burnDamage = 1;
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
    [SerializeField] int tentacleBodyDamage = 1;
    [SerializeField] int tentacleTipDamage = 2;
    [SerializeField] float tentacleKnockback = 10f;
    [SerializeField] bool tentacleBurns = true;
    [SerializeField] Color tentacleColor = new Color(0.08f, 0.05f, 0.12f);
    [SerializeField] Color tentacleTipColor = new Color(0.15f, 0.08f, 0.2f);

    [Header("2) Leap + Roll")]
    [SerializeField] bool useLeap = true;
    [SerializeField] float leapPoseTime = 0.1f;
    [SerializeField] Vector2 leapPoseScale = new Vector2(1.2f, 0.7f); // pose: crouches down
    [SerializeField] string leapAnimTrigger = "Leap";
    [SerializeField] float leapHeight = 7f;
    [SerializeField] float leapDuration = 1f;
    [SerializeField] float leapLandOffset = 3f;       // how far from the player it lands
    [SerializeField] float landingRadius = 3.5f;
    [SerializeField] int landingDamage = 2;
    [SerializeField] float landingKnockback = 12f;
    [SerializeField] bool landingBurns = true;
    [SerializeField] Color shockwaveColor = new Color(1f, 0.45f, 0.1f, 0.5f);
    [SerializeField] float shockwaveShowTime = 0.2f;
    [SerializeField] float rollPoseTime = 0.1f;
    [SerializeField] Vector2 rollPoseScale = new Vector2(0.8f, 0.8f); // pose: curls into a ball
    [SerializeField] string rollAnimTrigger = "Roll";
    [SerializeField] float rollSpeed = 14f;
    [SerializeField] float rollDistance = 12f;
    [SerializeField] int rollDamage = 2;
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
    [SerializeField] int spikeDamage = 2;
    [SerializeField] float spikeKnockback = 10f;
    [SerializeField] bool spikesBurn = true;
    [SerializeField] Color spikeColor = new Color(0.1f, 0.05f, 0.15f);

    [Header("Death (placeholder: crumbles)")]
    [SerializeField] int crumblePieces = 16;
    [SerializeField] float crumbleForce = 6f;
    [SerializeField] float crumbleLifetime = 1.5f;
    [SerializeField] float crumbleGravity = 2f;

    Rigidbody2D rb;
    BoxCollider2D body;
    SpriteRenderer sr;
    Health health;
    Transform player;
    Vector3 baseScale;
    Vector2 baseHalfSize;
    Vector2 pose = Vector2.one;
    bool movingSelf; // leaping or rolling: the boss is moving itself
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
        StartCoroutine(FightLoop());
    }

    void FixedUpdate()
    {
        // Stay planted on the ground so punches cannot knock it into the air (except while leaping/rolling)
        if (!movingSelf) rb.MovePosition(new Vector2(ClampX(rb.position.x), GroundCenterY));
    }

    Vector2 HalfSize => new Vector2(baseHalfSize.x * pose.x, baseHalfSize.y * pose.y);
    float GroundCenterY => groundY + HalfSize.y;
    float ClampX(float x) => Mathf.Clamp(x, arenaMinX + HalfSize.x, arenaMaxX - HalfSize.x);
    float DirToPlayer => player ? Mathf.Sign(player.position.x - rb.position.x) : -1f;

    IEnumerator FightLoop()
    {
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

            yield return new WaitForSeconds(restTime);
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
        yield return Pose(spikesPoseScale, spikesPoseTime, spikesAnimTrigger);

        float dir = DirToPlayer;
        float x = rb.position.x + dir * (HalfSize.x + spikeStartDistance);
        for (int i = 0; i < spikeCount; i++)
        {
            if (x < arenaMinX || x > arenaMaxX) break;
            StartCoroutine(OneSpike(x));
            x += dir * spikeSpacing;
            yield return new WaitForSeconds(spikeInterval);
        }

        yield return new WaitForSeconds(spikeUpTime + spikeHeight / Mathf.Max(spikeRiseSpeed, 0.01f));
        SetPose(Vector2.one);
    }

    IEnumerator OneSpike(float x)
    {
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
        pieceSr.sprite = sr ? sr.sprite : null;
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
        if (sr) sr.flipX = dir < 0f;
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
            psr.sprite = sr ? sr.sprite : null;
            psr.color = sr ? sr.color : Color.black;
            psr.sortingOrder = 3;
            var prb = piece.AddComponent<Rigidbody2D>();
            prb.gravityScale = crumbleGravity;
            prb.linearVelocity = new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)) * crumbleForce;
            prb.angularVelocity = Random.Range(-360f, 360f);
            Destroy(piece, crumbleLifetime);
        }

        if (!string.IsNullOrEmpty(sceneAfterDeath)) ScreenFade.FadeToScene(sceneAfterDeath);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, landingRadius);
    }
}

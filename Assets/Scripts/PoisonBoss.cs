using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Level 2 boss: a poison lizard that rules the dungeon under the poison tower.
// Attacks (picked at random, never the same twice in a row; each can be switched off under "Attacks Used"):
//   1) Lizard Dash - glows, then dashes at the player. If it hits: big damage (no poison) and he stops.
//                    If the player dodges: he crashes into the wall, is stunned, and poison bursts all over the arena.
//                    Phase 2: leaves a poison trail.
//   2) Slam        - leaps onto the player and lands with a poison splash. (off by default)
//   3) Rain        - poison drops fall from the ceiling all over the arena. (off by default)
//   4) Vomit       - sprays poison globs in an arc at the player; each leaves a puddle where it lands.
//   5) Claws       - when the player is close he steps in and swipes with his claws (off by default: he has contact damage instead).
// Below Phase 2 At (50% health) he gets faster and angrier, and poison keeps spawning all over the arena.
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
    [SerializeField] float restTime = 0.9f;
    [SerializeField, Range(0f, 1f)] float phase2At = 0.5f;
    [SerializeField] float phase2SpeedUp = 1.35f;

    [Header("Attacks Used")]
    [SerializeField] bool useCharge = true;
    [SerializeField] bool useSlam = false;
    [SerializeField] bool useRain = false;
    [SerializeField] bool useVomit = true;
    [SerializeField] bool useClaws = false;     // claws are off: touching him hurts instead

    [Header("Touch")]
    [SerializeField] bool touchHurts = true;    // ON: touching him hurts (contact damage)
    [SerializeField] bool touchPoisons = false; // ON: touching him also poisons
    [SerializeField] int touchDamage = 8;
    [SerializeField] float touchKnockback = 10f;

    [Header("1) Charge")]
    [SerializeField] float chargeWarnTime = 0.7f;
    [SerializeField] float chargeSpeed = 17f;
    [SerializeField] int chargeDamage = 14;      // (old touch-based charge damage, not used by the lizard dash)
    [SerializeField] int dashHitDamage = 18;     // lizard dash: hitting the player hurts a lot (no poison)
    [SerializeField] float dashKnockback = 16f;
    [SerializeField] float crashStunTime = 1.2f; // dodged: he hits the wall and is stunned this long
    [SerializeField] int crashPuddles = 8;       // dodged: poison puddles that appear all over the floor
    [SerializeField] int crashDrops = 10;        // dodged: poison drops that fall all over the arena

    [Header("2) Slam")]
    [SerializeField] float slamAirTime = 0.9f;
    [SerializeField] float slamHeight = 8f;
    [SerializeField] int slamDamage = 16;
    [SerializeField] float slamRadius = 3.5f;
    [SerializeField] float slamPuddleWidth = 7f;
    [SerializeField] float puddleLifetime = 4f;

    [Header("3) Rain")]
    [SerializeField] int rainDrops = 9;
    [SerializeField] float rainTime = 2.5f;

    [Header("4) Spit (old, replaced by Vomit)")]
    [SerializeField] Projectile spitPrefab;
    [SerializeField] int spitCount = 3;
    [SerializeField] float spitSpread = 18f; // degrees between globs

    [Header("4) Vomit")]
    [SerializeField] float vomitWarnTime = 0.5f;
    [SerializeField] int vomitGlobs = 7;
    [SerializeField] float vomitSpeed = 11f;
    [SerializeField] float vomitArc = 35f;       // degrees: globs spread between flat and this far up
    [SerializeField] int vomitDamage = 5;        // per glob, plus poison
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

    [Header("Phase 2 poison everywhere")]
    [SerializeField] float phase2PoisonInterval = 1.4f;
    [SerializeField] int phase2PuddlesPerWave = 2;
    [SerializeField] int phase2DropsPerWave = 2;

    [Header("Poison")]
    [SerializeField] int poisonDamage = 2;
    [SerializeField] float poisonDuration = 3f;

    [Header("On Death")]
    [SerializeField] GameObject healthBarRoot;  // hidden when he dies
    [SerializeField] GameObject[] removeOnDeath; // e.g. the exit gate
    [SerializeField] GameObject[] showOnDeath;   // e.g. the exit door

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
        startPos = rb.position;
        StartCoroutine(FightLoop());
    }

    void OnEnable()  => PlayerRespawn.Respawned += ResetFight;
    void OnDisable() => PlayerRespawn.Respawned -= ResetFight;

    void Update()
    {
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
            Tint(bodyColor);
            yield return new WaitForSeconds(restTime / Speed);
        }
    }

    // ---------------- 1) Charge ----------------

    IEnumerator Charge()
    {
        float dir = DirToPlayer;
        yield return Warn(chargeWarnTime / Speed);

        float targetX = dir > 0 ? arenaMaxX - halfWidth : arenaMinX + halfWidth;
        float nextTrail = rb.position.x;
        bool hitPlayer = false;
        while (Mathf.Abs(rb.position.x - targetX) > 0.05f)
        {
            float x = Mathf.MoveTowards(rb.position.x, targetX, chargeSpeed * Speed * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, rb.position.y));
            if (phase2 && Mathf.Abs(x - nextTrail) >= 2.5f) // phase 2: poison trail behind him
            {
                nextTrail = x;
                SpawnPuddle(x, 2.5f, puddleLifetime);
            }

            // Ran into the player: big hit, and the dash stops there
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(new Vector2(x, rb.position.y), new Vector2(halfWidth * 2f, halfHeight * 2f), 0f))
                if (HitPlayer(hit, dashHitDamage, new Vector2(dir, 0.6f).normalized * dashKnockback, false)) { hitPlayer = true; break; }
            if (hitPlayer) break;

            yield return new WaitForFixedUpdate();
        }
        if (hitPlayer) yield break;

        // The player dodged: he slams into the wall, is stunned, and poison bursts all over the arena
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.45f, 0.45f);
        for (int i = 0; i < crashPuddles; i++)
            SpawnPuddle(Random.Range(arenaMinX + 1f, arenaMaxX - 1f), Random.Range(2f, 3.5f), puddleLifetime);
        StartCoroutine(DropsAllOver(crashDrops, 1.2f));
        Tint(Color.Lerp(bodyColor, Color.gray, 0.5f)); // dazed
        yield return new WaitForSeconds(crashStunTime);
        Tint(bodyColor);
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
        yield return Warn(vomitWarnTime / Speed);
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
        Destroy(go, lifetime);
        spawned.Add(go);
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
        if (!health || health.CurrentHealth <= 0) return;
        StopAllCoroutines();
        ClearSpawned();
        health.Heal(health.MaxHealth);
        phase2 = false;
        lastAttack = -1;
        SetTouchDamage(touchDamage);
        rb.position = startPos;
        Tint(bodyColor);
        if (healthBarRoot) healthBarRoot.SetActive(true);
        StartCoroutine(FightLoop());
    }

    void OnDied()
    {
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

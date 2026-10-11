using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Plague Warden: a huge poison slime that rules the dungeon under the poison tower.
// Attacks (picked at random, never the same twice in a row):
//   1) Charge   - glows, then rams across the arena. Phase 2: leaves a poison trail.
//   2) Slam     - leaps onto the player and lands with a poison splash.
//   3) Rain     - poison drops fall from the ceiling all over the arena.
//   4) Spit     - spits a fan of globs at the player.
// Below Phase 2 At (50% health) he gets faster and angrier.
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

    [Header("Touch")]
    [SerializeField] int touchDamage = 10;
    [SerializeField] float touchKnockback = 10f;

    [Header("1) Charge")]
    [SerializeField] float chargeWarnTime = 0.7f;
    [SerializeField] float chargeSpeed = 17f;
    [SerializeField] int chargeDamage = 14;

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

    [Header("4) Spit")]
    [SerializeField] Projectile spitPrefab;
    [SerializeField] int spitCount = 3;
    [SerializeField] float spitSpread = 18f; // degrees between globs

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
        }
    }

    // ---------------- Fight loop ----------------

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(startDelay);
        while (true)
        {
            if (!player) { yield return null; continue; }

            int attack;
            do attack = Random.Range(0, 4); while (attack == lastAttack);
            lastAttack = attack;

            switch (attack)
            {
                case 0: yield return Charge(); break;
                case 1: yield return Slam(); break;
                case 2: yield return Rain(); break;
                default: yield return Spit(); break;
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

        SetTouchDamage(chargeDamage); // ramming hurts more than touching
        float targetX = dir > 0 ? arenaMaxX - halfWidth : arenaMinX + halfWidth;
        float nextTrail = rb.position.x;
        while (Mathf.Abs(rb.position.x - targetX) > 0.05f)
        {
            float x = Mathf.MoveTowards(rb.position.x, targetX, chargeSpeed * Speed * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, rb.position.y));
            if (phase2 && Mathf.Abs(x - nextTrail) >= 2.5f) // phase 2: poison trail behind him
            {
                nextTrail = x;
                SpawnPuddle(x, 2.5f, puddleLifetime);
            }
            yield return new WaitForFixedUpdate();
        }
        SetTouchDamage(touchDamage);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.25f, 0.25f); // hits the wall
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
        if (touchHazard) touchHazard.Setup(damage, touchKnockback, poisonDamage, poisonDuration, 0.5f, false);
    }

    bool HitPlayer(Collider2D hit, int damage, Vector2 knockback)
    {
        if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) return false;
        int before = h.CurrentHealth;
        h.TakeDamage(damage);
        if (h.CurrentHealth >= before) return false; // dashing or invincible
        if (hit.attachedRigidbody) hit.attachedRigidbody.linearVelocity = knockback;
        Poison.Apply(hit.gameObject, poisonDamage, poisonDuration, 0.5f);
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

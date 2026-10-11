using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Level 4 boss: the father. Phase 1 (phase 2 comes later).
// He glides across the flooded floor and releases lines of water waves toward the player,
// each wave in a line taller than the one before (small to tall). Every wave that hits soaks you:
// the Soaked counter stacks up, the damage grows, and the 4th soak is a really heavy hit (see Soaked).
// If the player dies, he heals fully and the fight starts over.
// Needs: Health and a BoxCollider2D (his body). Placeholder box look for now.
[RequireComponent(typeof(Health))]
public class FatherBoss : MonoBehaviour, IChainStunnable
{
    [Header("Arena")]
    [SerializeField] float groundY = 0f;        // top of the floor
    [SerializeField] float arenaMinX = 1f;      // he glides between these, and waves stop at them
    [SerializeField] float arenaMaxX = 39f;

    [Header("Flow")]
    [SerializeField] float startDelay = 2f;
    [SerializeField] float restTime = 1.5f;     // pause after each attack
    [SerializeField] float glideSpeed = 7f;     // how fast he moves over the water
    [SerializeField] float minGlide = 4f;       // he moves at least this far before each attack

    [Header("Wave lines")]
    [SerializeField] float[] waveHeights = { 1f, 2f, 3f, 4.5f }; // one line: small to tall, in this order
    [SerializeField] float waveWidth = 1.2f;
    [SerializeField] float waveSpeed = 9f;
    [SerializeField] float timeBetweenWaves = 0.45f; // gap between the waves in one line
    [SerializeField] float warnTime = 0.6f;          // he flashes before releasing a line
    [SerializeField] int linesPerAttack = 1;
    [SerializeField] float timeBetweenLines = 1.2f;

    [Header("Soaked (stacks on the player)")]
    [SerializeField] int[] soakDamage = { 5, 10, 15, 40 }; // damage of the 1st, 2nd, 3rd and 4th (heavy) soak
    [SerializeField] float firstSoakTime = 4f;   // timer after the first soak
    [SerializeField] float soakRefreshTime = 3f; // timer goes back to this on every soak after that

    [Header("Look (placeholder)")]
    [SerializeField] Color bodyColor = new Color(0.12f, 0.16f, 0.3f);
    [SerializeField] Color warnColor = new Color(0.4f, 0.75f, 1f);
    [SerializeField] Color waterColor = new Color(0.2f, 0.55f, 0.85f, 0.85f);
    [SerializeField] Color foamColor = new Color(0.85f, 0.95f, 1f, 0.95f);
    [SerializeField] GameObject healthBarRoot;   // boss bar, hidden when he dies

    [Header("Art (filled by Tools > Set Up KK Art)")]
    [SerializeField] SpriteRenderer artRenderer;  // child that shows him (the body box stays for hits)
    [SerializeField] SpriteRenderer surfRenderer; // child under his feet: the water he floats on while moving
    [SerializeField] Sprite[] idleFrames;
    [SerializeField] float idleFps = 6f;
    [SerializeField] Sprite[] attackFrames;       // raising his arms to call the waves
    [SerializeField] float attackFps = 10f;
    [SerializeField] Sprite[] surfFrames;         // movement: the water under him
    [SerializeField] float surfFps = 10f;
    [SerializeField] Sprite[] waveFrames;         // the wave drawings, small to tall
    [SerializeField] float waveArtScale = 1f;     // world size of the wave drawings

    Health health;
    SpriteRenderer sr;
    Transform player;
    float halfHeight;
    Vector3 startPos;
    float animTime;
    bool attackingPose, gliding;
    bool HasArt => artRenderer && idleFrames != null && idleFrames.Length > 0;
    readonly List<GameObject> spawned = new List<GameObject>();

    // Player's Siren's Chain: stunned, then slower for a while (see ChainStun)
    float slowUntil, slowFactor = 1f, resumeDelay = -1f;
    float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;

    void Awake()
    {
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        health.onDied.AddListener(OnDied);
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc) player = pc.transform;
        var body = GetComponent<BoxCollider2D>();
        halfHeight = body ? Mathf.Abs(transform.lossyScale.y) * body.size.y * 0.5f : 1f;
        startPos = transform.position;
        Tint(bodyColor);
        StartCoroutine(FightLoop());
    }

    // Idle when calm, the attack pose while calling waves, the water surf under him while he glides
    void Update()
    {
        if (!HasArt) return;
        animTime += Time.deltaTime;
        if (attackingPose && attackFrames != null && attackFrames.Length > 0)
            artRenderer.sprite = attackFrames[Mathf.Min(attackFrames.Length - 1, Mathf.FloorToInt(animTime * attackFps))];
        else
            artRenderer.sprite = idleFrames[Mathf.FloorToInt(animTime * idleFps) % idleFrames.Length];

        if (surfRenderer)
        {
            bool show = gliding && surfFrames != null && surfFrames.Length > 0;
            surfRenderer.enabled = show;
            if (show) surfRenderer.sprite = surfFrames[Mathf.FloorToInt(animTime * surfFps) % surfFrames.Length];
        }
    }

    void Pose(bool attack)
    {
        if (attackingPose == attack) return;
        attackingPose = attack;
        animTime = 0f;
    }

    void OnEnable()  => PlayerRespawn.Respawned += ResetFight;
    void OnDisable() => PlayerRespawn.Respawned -= ResetFight;

    // ---------------- Fight loop ----------------

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(resumeDelay >= 0f ? resumeDelay : startDelay); // after a chain stun: only the stun
        resumeDelay = -1f;
        while (true)
        {
            if (!player) { yield return null; continue; }
            yield return GlideToNewSpot();
            yield return WaveAttack();
            yield return new WaitForSeconds(restTime / SlowFactor);
        }
    }

    // Glide over the water to a new spot, keeping some distance from the player
    IEnumerator GlideToNewSpot()
    {
        float x = transform.position.x;
        float target = x;
        for (int tries = 0; tries < 10; tries++)
        {
            target = Random.Range(arenaMinX + 2f, arenaMaxX - 2f);
            if (Mathf.Abs(target - x) >= minGlide && Mathf.Abs(target - player.position.x) >= 6f) break;
        }
        gliding = true;
        if (artRenderer) artRenderer.flipX = false;
        while (Mathf.Abs(transform.position.x - target) > 0.05f)
        {
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, target, glideSpeed * SlowFactor * Time.deltaTime);
            p.y = groundY + halfHeight;
            transform.position = p;
            yield return null;
        }
        gliding = false;
    }

    // Lines of waves toward the player, small to tall
    IEnumerator WaveAttack()
    {
        Pose(true);
        for (int line = 0; line < linesPerAttack; line++)
        {
            yield return Warn(warnTime);
            float dir = player.position.x < transform.position.x ? -1f : 1f;
            float startX = transform.position.x + dir * (waveWidth + 0.5f);
            float stopX = dir > 0f ? arenaMaxX : arenaMinX;
            for (int w = 0; w < waveHeights.Length; w++)
            {
                SpawnWave(startX, dir, waveHeights[w], stopX, w);
                yield return new WaitForSeconds(timeBetweenWaves);
            }
            if (line < linesPerAttack - 1) yield return new WaitForSeconds(timeBetweenLines);
        }
        Pose(false);
    }

    void SpawnWave(float x, float dir, float height, float stopX, int index)
    {
        var go = new GameObject("WaterWave");
        go.transform.position = new Vector3(x, groundY, 0f);
        var wave = go.AddComponent<WaterWave>();
        wave.Setup(dir, waveSpeed, waveWidth, height, groundY, stopX,
            soakDamage, firstSoakTime, soakRefreshTime, sr ? sr.sprite : null, waterColor, foamColor);
        if (waveFrames != null && waveFrames.Length > 0) // small to tall: wave 1 uses the first drawing, and so on
            wave.UseArt(waveFrames[Mathf.Min(index, waveFrames.Length - 1)], waveArtScale);
        spawned.Add(go);
    }

    // ---------------- Helpers ----------------

    IEnumerator Warn(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Tint(Mathf.PingPong(t * 8f, 1f) > 0.5f ? warnColor : bodyColor);
            yield return null;
        }
        Tint(bodyColor);
    }

    void Tint(Color c)
    {
        if (sr) sr.color = c;
        if (HasArt) artRenderer.color = c == bodyColor ? Color.white : c; // warning flash shows on the drawing
    }

    void ClearSpawned()
    {
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }

    // ---------------- Reset and death ----------------


    // The player's Siren's Chain hit him: he stops what he is doing for stunTime, then is slower for slowTime
    public void ChainStun(float stunTime, float slowMultiplier, float slowTime)
    {
        if (!health || health.CurrentHealth <= 0) return;
        slowFactor = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
        slowUntil = Time.time + stunTime + slowTime;
        StopAllCoroutines();
        ClearSpawned();
        Tint(bodyColor);
        resumeDelay = stunTime;
        StartCoroutine(FightLoop());
    }

    void ResetFight()
    {
        if (!health || health.CurrentHealth <= 0) return;
        StopAllCoroutines();
        ClearSpawned();
        Tint(bodyColor);
        Pose(false);
        gliding = false;
        health.Heal(health.MaxHealth);
        transform.position = startPos;
        if (healthBarRoot) healthBarRoot.SetActive(true);
        StartCoroutine(FightLoop());
    }

    void OnDied()
    {
        StopAllCoroutines();
        ClearSpawned();
        if (healthBarRoot) healthBarRoot.SetActive(false);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.6f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        float x = transform.position.x + 2f;
        foreach (float h in waveHeights) { Gizmos.DrawLine(new Vector3(x, groundY), new Vector3(x, groundY + h)); x += 1.5f; }
    }
}

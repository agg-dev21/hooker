using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Level 4 boss: KK, the father.
// Phase 2 (below Phase 2 At health): he transforms into a monster (screen shake). He barely moves any more, but
// attacks much faster and harder: his waves come faster and hit harder, and his second attack is a horizontal
// blast from the monster's mouth: a red line follows you, locks, then the beam fires and keeps turning toward you.
// Phase 1:
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
    [SerializeField] float restTime = 0.8f;     // pause after each attack
    [SerializeField] float glideSpeed = 9f;     // how fast he moves over the water
    [SerializeField] float minGlide = 4f;       // he moves at least this far before each attack

    [Header("Wave lines")]
    [SerializeField] float[] waveHeights = { 1f, 2f, 3f, 4.5f }; // one line: small to tall, in this order
    [SerializeField] float waveWidth = 1.2f;
    [SerializeField] float waveSpeed = 12f;
    [SerializeField] float timeBetweenWaves = 0.35f; // gap between the waves in one line
    [SerializeField] float warnTime = 0.45f;         // he flashes before releasing a line
    [SerializeField] int linesPerAttack = 2;
    [SerializeField] float timeBetweenLines = 0.9f;
    [SerializeField] float waveKnockback = 12f;      // a hit pushes you along with the wave
    [SerializeField] float waveSlow = 0.5f;          // and slows you down (0.5 = half speed)...
    [SerializeField] float waveSlowTime = 1.5f;      // ...for this long

    [Header("Soaked (stacks on the player)")]
    [SerializeField] int[] soakDamage = { 8, 14, 20, 50 }; // damage of the 1st, 2nd, 3rd and 4th (heavy) soak
    [SerializeField] float firstSoakTime = 4f;   // timer after the first soak
    [SerializeField] float soakRefreshTime = 3f; // timer goes back to this on every soak after that

    [Header("Look (placeholder)")]
    [SerializeField] Color bodyColor = new Color(0.12f, 0.16f, 0.3f);
    [SerializeField] Color warnColor = new Color(0.4f, 0.75f, 1f);
    [SerializeField] Color waterColor = new Color(0.2f, 0.55f, 0.85f, 0.85f);
    [SerializeField] Color foamColor = new Color(0.85f, 0.95f, 1f, 0.95f);
    [SerializeField] GameObject healthBarRoot;   // boss bar, hidden when he dies

    [Header("Art (filled by Tools > Art > KK)")]
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

    [Header("Phase 2: monster form and blast")]
    [SerializeField, Range(0f, 1f)] float phase2At = 0.5f; // transforms below this much health
    [SerializeField] Sprite[] phase2Frames;       // the monster form (filled by Tools > Art > KK)
    [SerializeField] float phase2Fps = 8f;
    [SerializeField, Range(0f, 1f)] float blastChance = 0.5f; // in phase 2: chance an attack is the blast instead of waves
    [SerializeField] float phase2AttackSpeed = 1.7f;   // attacks, warnings, rests and waves this much faster
    [SerializeField] float phase2Damage = 1.5f;        // waves (soaks) do this much more damage
    [SerializeField, Range(0f, 1f)] float phase2MoveChance = 0.2f; // he only moves before this share of his attacks...
    [SerializeField] float phase2MaxMove = 3f;          // ...and only this far
    [SerializeField] Sprite[] blastFrames;        // the beam, growing (drawn pointing right)
    [SerializeField] float blastFps = 14f;
    [SerializeField] float blastWarnTime = 0.75f; // the red line follows you...
    [SerializeField] float blastLockTime = 0.3f;  // ...then stays put this long before it fires
    [SerializeField] float blastThickness = 1.6f;
    [SerializeField] Vector2 mouthOffset = new Vector2(1.6f, 3.5f); // the monster's mouth: from his middle (x) and his feet (y)
    [SerializeField] float blastTurnSpeed = 35f;  // degrees a second the beam turns to follow you while it fires
    [SerializeField] float blastLength = 42f;
    [SerializeField] float blastHoldTime = 0.35f; // the full beam stays this long
    [SerializeField] int blastDamage = 22;
    [SerializeField] float blastKnockback = 14f;
    [SerializeField] Color blastWarnColor = new Color(1f, 0.15f, 0.25f, 0.45f);
    [SerializeField] Color blastColor = new Color(1f, 0.45f, 0.35f, 0.9f);  // (no art) plain beam colour

    Health health;
    SpriteRenderer sr;
    Transform player;
    float halfHeight;
    Vector3 startPos;
    float animTime;
    bool attackingPose, gliding;
    bool HasArt => artRenderer && idleFrames != null && idleFrames.Length > 0;
    bool phase2;
    float Fast => phase2 ? Mathf.Max(phase2AttackSpeed, 0.1f) : 1f; // phase 2: everything about attacking is quicker
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
        if (!phase2 && health && health.CurrentHealth > 0 && health.CurrentHealth <= health.MaxHealth * phase2At) Transform2();
        if (!HasArt) return;
        animTime += Time.deltaTime;
        if (phase2 && phase2Frames != null && phase2Frames.Length > 0)
        {
            artRenderer.sprite = phase2Frames[Mathf.FloorToInt(animTime * phase2Fps) % phase2Frames.Length];
            FacePlayer();
        }
        else if (attackingPose && attackFrames != null && attackFrames.Length > 0)
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
            while (DialogueManager.Busy) yield return null; // people are talking: no attacks
            if (!player) { yield return null; continue; }
            if (!phase2 || Random.value < phase2MoveChance) yield return GlideToNewSpot(); // phase 2: he barely moves
            if (phase2 && Random.value < blastChance) yield return Blast();
            else yield return WaveAttack();
            yield return new WaitForSeconds(restTime / Fast / SlowFactor);
        }
    }

    // Glide over the water to a new spot, keeping some distance from the player
    IEnumerator GlideToNewSpot()
    {
        float x = transform.position.x;
        float target = x;
        for (int tries = 0; tries < 10; tries++)
        {
            target = phase2 ? Mathf.Clamp(x + Random.Range(-phase2MaxMove, phase2MaxMove), arenaMinX + 2f, arenaMaxX - 2f)
                            : Random.Range(arenaMinX + 2f, arenaMaxX - 2f);
            if (phase2 || (Mathf.Abs(target - x) >= minGlide && Mathf.Abs(target - player.position.x) >= 6f)) break;
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
            yield return Warn(warnTime / Fast);
            float dir = player.position.x < transform.position.x ? -1f : 1f;
            float startX = transform.position.x + dir * (waveWidth + 0.5f);
            float stopX = dir > 0f ? arenaMaxX : arenaMinX;
            for (int w = 0; w < waveHeights.Length; w++)
            {
                SpawnWave(startX, dir, waveHeights[w], stopX, w);
                yield return new WaitForSeconds(timeBetweenWaves / Fast);
            }
            if (line < linesPerAttack - 1) yield return new WaitForSeconds(timeBetweenLines / Fast);
        }
        Pose(false);
    }

    void SpawnWave(float x, float dir, float height, float stopX, int index)
    {
        var go = new GameObject("WaterWave");
        go.transform.position = new Vector3(x, groundY, 0f);
        var wave = go.AddComponent<WaterWave>();
        int[] damage = soakDamage;
        if (phase2)
        {
            damage = new int[soakDamage.Length];
            for (int i = 0; i < damage.Length; i++) damage[i] = Mathf.RoundToInt(soakDamage[i] * phase2Damage);
        }
        wave.Setup(dir, waveSpeed * Fast, waveWidth, height, groundY, stopX,
            damage, firstSoakTime, soakRefreshTime, sr ? sr.sprite : null, waterColor, foamColor);
        wave.SetHitEffects(waveKnockback, waveSlow, waveSlowTime);
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

    // ---------------- Phase 2 ----------------

    void Transform2()
    {
        phase2 = true;
        animTime = 0f;
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.7f, 0.4f);
        StartCoroutine(TransformFlash());
    }

    IEnumerator TransformFlash()
    {
        for (int i = 0; i < 6; i++)
        {
            if (artRenderer) artRenderer.color = i % 2 == 0 ? Color.red : Color.white;
            yield return new WaitForSeconds(0.08f);
        }
        if (artRenderer) artRenderer.color = Color.white;
    }

    // Where the beam comes out: the monster's mouth (it flips with him)
    Vector2 Mouth()
    {
        bool left = player && player.position.x < transform.position.x;
        float feet = transform.position.y - halfHeight;
        return new Vector2(transform.position.x + (left ? -mouthOffset.x : mouthOffset.x), feet + mouthOffset.y);
    }

    // Phase 2 blast: a red line from his mouth follows the player, locks for a moment, then the beam fires
    // from the mouth and keeps turning toward the player (a bit slower than they can run).
    IEnumerator Blast()
    {
        Pose(true);
        FacePlayer();
        Vector2 from = Mouth();
        Vector2 aim = AimAt(from);

        GameObject warn = BeamBox("BlastWarn", blastWarnColor);
        float warnFor = blastWarnTime / Fast, lockFor = blastLockTime / Fast;
        for (float t = 0f; t < warnFor + lockFor; t += Time.deltaTime)
        {
            FacePlayer();
            from = Mouth();
            if (t < warnFor) aim = AimAt(from); // follows you, then locks
            PlaceBeam(warn.transform, from, aim, blastLength, 0.25f);
            Tint(Mathf.PingPong(t * 8f, 1f) > 0.5f ? warnColor : bodyColor);
            yield return null;
        }
        Tint(bodyColor);
        Destroy(warn);

        // The beam
        bool art = blastFrames != null && blastFrames.Length > 0;
        GameObject beam;
        SpriteRenderer bsr = null;
        if (art)
        {
            beam = new GameObject("Blast");
            bsr = beam.AddComponent<SpriteRenderer>();
            bsr.sortingOrder = 9;
            spawned.Add(beam);
        }
        else beam = BeamBox("Blast", blastColor);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.3f, 0.3f);

        bool hit = false;
        int frames = art ? blastFrames.Length : 1;
        float frameTime = 1f / Mathf.Max(blastFps, 0.01f);
        float total = frames * frameTime + blastHoldTime;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            FacePlayer();
            from = Mouth();
            // Keeps turning toward the player, at most Blast Turn Speed degrees a second
            aim = Vector3.RotateTowards(aim, AimAt(from), blastTurnSpeed * Fast * Mathf.Deg2Rad * Time.deltaTime, 0f);
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            if (art)
            {
                bsr.sprite = blastFrames[Mathf.Min(frames - 1, Mathf.FloorToInt(t / frameTime))];
                Vector2 full = bsr.sprite.rect.size / bsr.sprite.pixelsPerUnit;
                beam.transform.position = from;                       // the drawing starts at its left edge: the mouth
                beam.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                beam.transform.localScale = new Vector3(blastLength / Mathf.Max(full.x, 0.01f), blastThickness * 1.4f / Mathf.Max(full.y, 0.01f), 1f);
            }
            else PlaceBeam(beam.transform, from, aim, blastLength, blastThickness);

            // Hits once, only once the beam has grown out
            if (!hit && t >= frameTime * Mathf.Max(1, frames / 2))
            {
                Vector2 center = from + aim * blastLength * 0.5f;
                foreach (Collider2D c in Physics2D.OverlapBoxAll(center, new Vector2(blastLength, blastThickness), angle))
                {
                    if (!c.TryGetComponent(out PlayerController2D _) || !c.TryGetComponent(out Health h)) continue;
                    int before = h.CurrentHealth;
                    h.TakeDamage(blastDamage);
                    if (h.CurrentHealth < before && c.attachedRigidbody)
                        c.attachedRigidbody.linearVelocity = aim * blastKnockback + Vector2.up * blastKnockback * 0.3f;
                    hit = true;
                    break;
                }
            }
            yield return null;
        }
        spawned.Remove(beam);
        Destroy(beam);
        Pose(false);
    }

    Vector2 AimAt(Vector2 from)
    {
        Vector2 to = player ? (Vector2)player.position - from : Vector2.right;
        return to.sqrMagnitude < 0.01f ? Vector2.right : to.normalized;
    }

    // The monster faces the player (his drawing faces right)
    void FacePlayer()
    {
        if (phase2 && artRenderer && player) artRenderer.flipX = player.position.x < transform.position.x;
    }

    GameObject BeamBox(string name, Color color)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sr ? sr.sprite : null;
        r.color = color;
        r.sortingOrder = 9;
        spawned.Add(go);
        return go;
    }

    // A plain box beam starting at "from" going along "dir" (any angle)
    static void PlaceBeam(Transform t, Vector2 from, Vector2 dir, float length, float thickness)
    {
        t.position = from + dir * length * 0.5f;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(length, thickness, 1f);
    }

    void ResetFight()
    {
        if (!health || health.CurrentHealth <= 0) return;
        StopAllCoroutines();
        ClearSpawned();
        Tint(bodyColor);
        Pose(false);
        gliding = false;
        phase2 = false;
        if (artRenderer) artRenderer.color = Color.white;
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

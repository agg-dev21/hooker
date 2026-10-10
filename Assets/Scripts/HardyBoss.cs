using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// HARDY: Level 1 ka boss (Venom jaisa). Teen attacks, random order (same attack do baar lagatar nahi):
// 1) Tentacle Lash: tentacle zameen ke saath player ki taraf. Body = kam damage, tip = zyada.
// 2) Leap + Roll: upar uchhal ke player ke paas girta hai (shockwave AOE), phir player ki taraf rolls.
// 3) Spikes: zameen se spikes ek ke baad ek player ki taraf nikalte hain.
// Koi warning/flash nahi: har attack se pehle boss ek pose leta hai (abhi box ka shape badalta hai,
// baad mein art aaye toh Animator trigger chala sakte ho). Marne pe tukde tukde ho ke bikhar jaata hai.
// Har value Inspector mein. Chhoone ka damage (ContactDamage) alag script hai, usme aag nahi.
// Object pe chahiye: SpriteRenderer, BoxCollider2D (solid), Rigidbody2D (Kinematic), Health.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
public class HardyBoss : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] float groundY = -5f;     // deck ki surface
    [SerializeField] float arenaMinX = -13f;  // deck ka left end
    [SerializeField] float arenaMaxX = 39f;   // deck ka right end

    [Header("Fight Flow")]
    [SerializeField] float startDelay = 1.5f; // scene shuru hone ke baad pehla attack
    [SerializeField] float restTime = 1.2f;   // do attacks ke beech
    [SerializeField] GameObject healthBarRoot; // boss marne pe chhupa do
    [SerializeField] string sceneAfterDeath;   // khaali = kuch nahi, bhara = fade karke ye scene
    [SerializeField] Animator animator;        // optional: art aaye toh pose triggers isme jaate hain

    [Header("Fire (used by attacks that burn)")]
    [SerializeField] int burnDamage = 1;
    [SerializeField] float burnTickInterval = 1f;
    [SerializeField] float burnDuration = 3f;

    [Header("1) Tentacle Lash")]
    [SerializeField] bool useTentacle = true;
    [SerializeField] float tentaclePoseTime = 0.1f;            // attack se pehle pose kitni der
    [SerializeField] Vector2 tentaclePoseScale = new Vector2(1.3f, 0.85f); // pose: thoda chauda aur neecha
    [SerializeField] string tentacleAnimTrigger = "Tentacle";
    [SerializeField] float tentacleLength = 14f;
    [SerializeField] float tentacleTipLength = 2f;
    [SerializeField] float tentacleThickness = 0.8f;
    [SerializeField] float tentacleHeight = 1f;       // zameen se kitna upar
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
    [SerializeField] Vector2 leapPoseScale = new Vector2(1.2f, 0.7f); // pose: jhuk ke baith jaata hai
    [SerializeField] string leapAnimTrigger = "Leap";
    [SerializeField] float leapHeight = 7f;
    [SerializeField] float leapDuration = 1f;
    [SerializeField] float leapLandOffset = 3f;       // player se kitna door land kare
    [SerializeField] float landingRadius = 3.5f;
    [SerializeField] int landingDamage = 2;
    [SerializeField] float landingKnockback = 12f;
    [SerializeField] bool landingBurns = true;
    [SerializeField] Color shockwaveColor = new Color(1f, 0.45f, 0.1f, 0.5f);
    [SerializeField] float shockwaveShowTime = 0.2f;
    [SerializeField] float rollPoseTime = 0.1f;
    [SerializeField] Vector2 rollPoseScale = new Vector2(0.8f, 0.8f); // pose: gola ban jaata hai
    [SerializeField] string rollAnimTrigger = "Roll";
    [SerializeField] float rollSpeed = 14f;
    [SerializeField] float rollDistance = 12f;
    [SerializeField] int rollDamage = 2;
    [SerializeField] float rollKnockback = 12f;
    [SerializeField] bool rollBurns = true;

    [Header("3) Spikes")]
    [SerializeField] bool useSpikes = true;
    [SerializeField] float spikesPoseTime = 0.1f;
    [SerializeField] Vector2 spikesPoseScale = new Vector2(0.85f, 1.25f); // pose: lamba khada ho jaata hai
    [SerializeField] string spikesAnimTrigger = "Spikes";
    [SerializeField] int spikeCount = 6;
    [SerializeField] float spikeStartDistance = 2f;   // boss se pehla spike kitna door
    [SerializeField] float spikeSpacing = 2.2f;
    [SerializeField] float spikeInterval = 0.15f;     // ek spike se agle tak
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
    bool movingSelf; // leap ya roll: boss khud move kar raha hai
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
        // Punch ke dhakke se hawa mein na tairey: zameen pe khada raho (jab tak leap/roll nahi)
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
            if (options.Count > 1) options.Remove(lastAttack); // same attack do baar nahi
            if (options.Count == 0 || !player) { yield return null; continue; }

            int attack = options[Random.Range(0, options.Count)];
            lastAttack = attack;

            if (attack == 0) yield return TentacleLash();
            else if (attack == 1) yield return LeapAndRoll();
            else yield return Spikes();

            yield return new WaitForSeconds(restTime);
        }
    }

    // ---------------- Pose (attack se pehle) ----------------
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
        if (!movingSelf) rb.position = new Vector2(ClampX(rb.position.x), GroundCenterY); // pair zameen pe
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

        // Player ke paas land karo (thoda pehle), arena ke andar
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
        body.isTrigger = true; // rolling: player dash karke aar paar ja sakta hai
        movingSelf = true;

        float rolled = 0f;
        while (rolled < rollDistance)
        {
            float step = rollSpeed * Time.fixedDeltaTime;
            float nextX = ClampX(rb.position.x + dir * step);
            if (Mathf.Approximately(nextX, rb.position.x)) break; // deewar tak pahunch gaye
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

    // Ek box jo boss ke sprite se bana hai. hurts = true: trigger + DamageZone
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

        // Tukde: boss ke size ke andar random chhote box, idhar udhar gir ke gayab
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

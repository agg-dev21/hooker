using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Level 3 boss: the Chain Siren, half woman, half octopus. She never moves: she sits on her throne
// on the left side of the arena and attacks with iron chains. Every attack is telegraphed (red warning lines).
//   1) Low Lash   - a chain sweeps along the floor across the whole arena: JUMP over it.
//   2) High Lash  - a chain sweeps at head height + 1: stay on the ground (don't jump into it).
//   3) Chain Rain - chains slam down from the ceiling where the red marks are.
//   4) Chain Grab - a chain shoots straight at you. Hit = damage and you get yanked toward her. Dash or hook away.
//   5) Anchor     - she throws an anchor that lands on you with a shockwave.
// Phase 2 (below half health): everything is faster, lashes come in pairs, more chains rain, two grabs in a row.
// Touching her tentacles hurts. If the player dies she heals fully and the fight starts over.
// She can fight together with the lizard (PoisonBoss): they take turns attacking (BossTurns).
[RequireComponent(typeof(Health))]
public class OctopusBoss : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] float groundY = 0f;      // top of the floor
    [SerializeField] float ceilingY = 18f;    // chain rain comes from here
    [SerializeField] float arenaMaxX = 33f;   // right wall: lashes reach this far
    [SerializeField] float rainMinX = 9f;     // chain rain lands between these
    [SerializeField] float rainMaxX = 32f;
    [SerializeField] Transform hands;         // where her chains come from

    [Header("Flow")]
    [SerializeField] float startDelay = 2f;
    [SerializeField] float restTime = 0.8f;
    [SerializeField, Range(0f, 1f)] float phase2At = 0.5f;
    [SerializeField] float phase2SpeedUp = 1.3f;
    [SerializeField] float maxTurnWait = 2f;   // with a second boss: longest she waits for it to finish attacking

    [Header("Lashes")]
    [SerializeField] float lashWarnTime = 0.75f;
    [SerializeField] float lashSpeed = 40f;
    [SerializeField] float lowLashY = 0.6f;    // above the floor: jump over it
    [SerializeField] float highLashY = 3.1f;   // above a standing player's head: don't jump
    [SerializeField] int lashDamage = 12;

    [Header("Chain Rain")]
    [SerializeField] int rainChains = 4;
    [SerializeField] float rainWarnTime = 0.8f;
    [SerializeField] int rainDamage = 14;

    [Header("Chain Grab")]
    [SerializeField] float grabWarnTime = 0.6f;
    [SerializeField] float grabSpeed = 35f;
    [SerializeField] float grabRange = 32f;
    [SerializeField] int grabDamage = 10;
    [SerializeField] float grabPull = 16f;     // how hard you get yanked toward her

    [Header("Anchor")]
    [SerializeField] float anchorAirTime = 1.1f;
    [SerializeField] float anchorHeight = 9f;
    [SerializeField] float anchorRadius = 2.5f;
    [SerializeField] int anchorDamage = 16;

    [Header("Touch")]
    [SerializeField] int touchDamage = 10;
    [SerializeField] float touchKnockback = 14f;

    [Header("On Death")]
    [SerializeField] GameObject healthBarRoot;
    [SerializeField] GameObject[] removeOnDeath;
    [SerializeField] GameObject[] showOnDeath;

    [Header("Look")]
    [SerializeField] Sprite boxSprite;         // plain square used for chains and warnings
    [SerializeField] Color chainColor = new Color(0.5f, 0.5f, 0.56f);
    [SerializeField] Color warnColor = new Color(1f, 0.15f, 0.15f, 0.45f);
    [SerializeField] Color flashColor = new Color(1f, 0.6f, 0.6f);
    [SerializeField] SpriteRenderer[] bodyParts; // flash these before each attack

    Health health;
    Transform player;
    Rigidbody2D playerRb;
    bool phase2;
    int lastAttack = -1;
    Color[] partColors;
    readonly List<GameObject> spawned = new List<GameObject>();

    float Speed => phase2 ? phase2SpeedUp : 1f;
    Vector2 HandPos => hands ? (Vector2)hands.position : (Vector2)transform.position;

    void Awake()
    {
        health = GetComponent<Health>();
        health.onDied.AddListener(OnDied);
        if (!boxSprite && TryGetComponent(out SpriteRenderer sr)) boxSprite = sr.sprite;
        if (bodyParts == null) bodyParts = new SpriteRenderer[0];
        partColors = new Color[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++) if (bodyParts[i]) partColors[i] = bodyParts[i].color;
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc) { player = pc.transform; playerRb = pc.GetComponent<Rigidbody2D>(); }

        // Touching her hurts and pushes you away (to the right)
        var touch = new GameObject("TouchHitbox");
        touch.transform.SetParent(transform, false);
        var tc = touch.AddComponent<BoxCollider2D>();
        tc.isTrigger = true;
        if (TryGetComponent(out BoxCollider2D myBox)) { tc.size = myBox.size; tc.offset = myBox.offset; }
        touch.AddComponent<DamageZone>().Setup(touchDamage, touchKnockback, false, 0, 0f, 1f);

        StartCoroutine(FightLoop());
    }

    void OnEnable()  => PlayerRespawn.Respawned += ResetFight;
    void OnDisable() { PlayerRespawn.Respawned -= ResetFight; BossTurns.End(this); }

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

            // Fighting together with another boss: wait (up to Max Turn Wait) while it attacks
            for (float w = 0f; w < maxTurnWait && BossTurns.OtherIsAttacking(this); w += Time.deltaTime) yield return null;
            BossTurns.Begin(this);

            int attack;
            do attack = Random.Range(0, 5); while (attack == lastAttack);
            lastAttack = attack;

            switch (attack)
            {
                case 0: yield return Lash(lowLashY); if (phase2) yield return Lash(highLashY); break;
                case 1: yield return Lash(highLashY); if (phase2) yield return Lash(lowLashY); break;
                case 2: yield return ChainRain(); break;
                case 3: yield return Grab(); if (phase2) yield return Grab(); break;
                default: yield return Anchor(); break;
            }
            BossTurns.End(this);
            yield return new WaitForSeconds(restTime / Speed);
        }
    }

    // ---------------- 1 + 2) Lashes ----------------

    IEnumerator Lash(float heightAboveFloor)
    {
        float y = groundY + heightAboveFloor;
        Vector2 from = new Vector2(HandPos.x, y);
        float length = arenaMaxX - from.x;

        GameObject warn = Bar(from, Vector2.right, length, 0.5f, warnColor, false);
        yield return Flash(lashWarnTime / Speed);
        Destroy(warn);

        Chain chain = new Chain(this, from, Vector2.right, 0.45f, lashDamage, 10f);
        yield return chain.Extend(length, lashSpeed * Speed);
        yield return new WaitForSeconds(0.15f);
        yield return chain.Retract(lashSpeed * 0.6f);
    }

    // ---------------- 3) Chain Rain ----------------

    IEnumerator ChainRain()
    {
        int count = phase2 ? rainChains + 2 : rainChains;
        var xs = new List<float>();
        if (player) xs.Add(Mathf.Clamp(player.position.x, rainMinX, rainMaxX)); // one always aimed at you
        while (xs.Count < count)
        {
            float x = Random.Range(rainMinX, rainMaxX);
            bool tooClose = false;
            foreach (float other in xs) if (Mathf.Abs(other - x) < 2.5f) tooClose = true;
            if (!tooClose) xs.Add(x);
        }

        var warns = new List<GameObject>();
        foreach (float x in xs) warns.Add(Bar(new Vector2(x, ceilingY), Vector2.down, ceilingY - groundY, 0.6f, warnColor, false));
        yield return Flash(rainWarnTime / Speed);
        foreach (GameObject w in warns) Destroy(w);

        var chains = new List<Chain>();
        foreach (float x in xs) chains.Add(new Chain(this, new Vector2(x, ceilingY), Vector2.down, 0.55f, rainDamage, 8f));
        float len = 0f, full = ceilingY - groundY;
        while (len < full)
        {
            len = Mathf.MoveTowards(len, full, 45f * Speed * Time.deltaTime);
            foreach (Chain c in chains) c.SetLength(len);
            yield return null;
        }
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.2f, 0.2f);
        yield return new WaitForSeconds(0.35f);
        while (len > 0f)
        {
            len = Mathf.MoveTowards(len, 0f, 18f * Time.deltaTime);
            foreach (Chain c in chains) c.SetLength(len);
            yield return null;
        }
        foreach (Chain c in chains) c.Destroy();
    }

    // ---------------- 4) Chain Grab ----------------

    IEnumerator Grab()
    {
        Vector2 from = HandPos;
        Vector2 dir = ((Vector2)player.position - from).normalized;

        // Aim line follows you for a moment, then locks
        GameObject warn = Bar(from, dir, grabRange, 0.25f, warnColor, false);
        for (float t = 0f; t < grabWarnTime / Speed; t += Time.deltaTime)
        {
            if (t < grabWarnTime / Speed * 0.6f && player)
            {
                dir = ((Vector2)player.position - from).normalized;
                warn.transform.right = dir;
            }
            FlashParts(Mathf.PingPong(t * 8f, 1f) > 0.5f);
            yield return null;
        }
        FlashParts(false);
        Destroy(warn);

        Chain chain = new Chain(this, from, dir, 0.4f, 0, 0f); // damage handled below (it pulls you instead of pushing)
        bool caught = false;
        float len = 0f;
        while (len < grabRange && !caught)
        {
            len = Mathf.MoveTowards(len, grabRange, grabSpeed * Speed * Time.deltaTime);
            chain.SetLength(len);
            Vector2 tip = from + dir * len;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(tip, 0.6f))
            {
                if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;
                int before = h.CurrentHealth;
                h.TakeDamage(grabDamage);
                if (h.CurrentHealth < before && playerRb)
                {
                    caught = true;
                    Vector2 pull = (from - (Vector2)hit.transform.position).normalized;
                    playerRb.linearVelocity = new Vector2(pull.x, Mathf.Max(pull.y, 0.3f)) * grabPull;
                }
                break;
            }
            if (Physics2D.OverlapPoint(tip, LayerMask.GetMask("Ground"))) break; // hit a wall
            yield return null;
        }
        yield return chain.Retract(30f);
    }

    // ---------------- 5) Anchor ----------------

    IEnumerator Anchor()
    {
        yield return Flash(0.5f / Speed);
        Vector2 from = HandPos + Vector2.up;
        float landX = player ? player.position.x : (rainMinX + rainMaxX) * 0.5f;
        Vector2 to = new Vector2(landX, groundY + 0.6f);

        GameObject mark = Bar(new Vector2(landX - anchorRadius, groundY + 0.15f), Vector2.right, anchorRadius * 2f, 0.3f, warnColor, false);
        GameObject anchor = Bar(from, Vector2.right, 1.2f, 1.2f, chainColor, false);
        float time = anchorAirTime / Speed;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            Vector2 p = Vector2.Lerp(from, to, k) + Vector2.up * (4f * anchorHeight * k * (1f - k));
            anchor.transform.position = p - Vector2.right * 0.6f;
            anchor.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
            yield return null;
        }
        Destroy(mark);
        Destroy(anchor);

        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.3f, 0.35f);
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(to, anchorRadius))
        {
            if (!hit.TryGetComponent(out PlayerController2D _) || !hit.TryGetComponent(out Health h)) continue;
            int before = h.CurrentHealth;
            h.TakeDamage(anchorDamage);
            if (h.CurrentHealth < before && hit.attachedRigidbody)
                hit.attachedRigidbody.linearVelocity = new Vector2(Mathf.Sign(hit.transform.position.x - landX), 0.8f) * 12f;
            break;
        }
    }

    // ---------------- Helpers ----------------

    IEnumerator Flash(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            FlashParts(Mathf.PingPong(t * 8f, 1f) > 0.5f);
            yield return null;
        }
        FlashParts(false);
    }

    void FlashParts(bool on)
    {
        for (int i = 0; i < bodyParts.Length; i++)
            if (bodyParts[i]) bodyParts[i].color = on ? flashColor * partColors[i] : partColors[i];
    }

    // A bar from 'from' going 'length' in direction 'dir' (warnings, anchor). Optional hitbox.
    GameObject Bar(Vector2 from, Vector2 dir, float length, float thickness, Color color, bool hurts)
    {
        var root = new GameObject("BossBar");
        root.transform.position = from;
        root.transform.right = dir;
        var piece = new GameObject("Piece");
        piece.transform.SetParent(root.transform, false);
        piece.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        piece.transform.localScale = new Vector3(length, thickness, 1f);
        var sr = piece.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = color;
        sr.sortingOrder = 8;
        spawned.Add(root);
        return root;
    }

    // An iron chain that grows out of a point. Damages (and knocks back) the player while out.
    class Chain
    {
        readonly OctopusBoss boss;
        readonly Transform root;
        readonly DamageZone zone;

        public Chain(OctopusBoss boss, Vector2 from, Vector2 dir, float thickness, int damage, float knockback)
        {
            this.boss = boss;
            var go = new GameObject("BossChain");
            root = go.transform;
            root.position = from;
            root.right = dir;
            var links = new GameObject("Links");
            links.transform.SetParent(root, false);
            links.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            links.transform.localScale = new Vector3(1f, thickness, 1f);
            var sr = links.AddComponent<SpriteRenderer>();
            sr.sprite = boss.boxSprite;
            sr.color = boss.chainColor;
            sr.sortingOrder = 9;
            if (damage > 0)
            {
                var col = links.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                zone = links.AddComponent<DamageZone>();
                zone.Setup(damage, knockback, false, 0, 0f, 1f);
            }
            boss.spawned.Add(go);
            SetLength(0f);
        }

        public void SetLength(float len)
        {
            if (!root) return;
            root.localScale = new Vector3(Mathf.Max(len, 0.001f), 1f, 1f);
            if (zone) zone.Active = len > 0.3f;
        }

        public IEnumerator Extend(float length, float speed)
        {
            float len = 0f;
            while (len < length) { len = Mathf.MoveTowards(len, length, speed * Time.deltaTime); SetLength(len); yield return null; }
        }

        public IEnumerator Retract(float speed)
        {
            float len = root ? root.localScale.x : 0f;
            while (len > 0f) { len = Mathf.MoveTowards(len, 0f, speed * Time.deltaTime); SetLength(len); yield return null; }
            Destroy();
        }

        public void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
        }
    }

    void ClearSpawned()
    {
        foreach (GameObject go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }

    // ---------------- Reset and death ----------------

    void ResetFight()
    {
        BossTurns.End(this);
        if (!health || health.CurrentHealth <= 0) return;
        StopAllCoroutines();
        ClearSpawned();
        FlashParts(false);
        health.Heal(health.MaxHealth);
        phase2 = false;
        lastAttack = -1;
        if (healthBarRoot) healthBarRoot.SetActive(true);
        StartCoroutine(FightLoop());
    }

    void OnDied()
    {
        BossTurns.End(this);
        StopAllCoroutines();
        ClearSpawned();
        if (healthBarRoot) healthBarRoot.SetActive(false);
        foreach (GameObject go in removeOnDeath) if (go) go.SetActive(false);
        foreach (GameObject go in showOnDeath) if (go) go.SetActive(true);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.6f, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(HandPos.x, groundY + lowLashY), new Vector3(arenaMaxX, groundY + lowLashY));
        Gizmos.DrawLine(new Vector3(HandPos.x, groundY + highLashY), new Vector3(arenaMaxX, groundY + highLashY));
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(rainMinX, ceilingY), new Vector3(rainMaxX, ceilingY));
    }
}
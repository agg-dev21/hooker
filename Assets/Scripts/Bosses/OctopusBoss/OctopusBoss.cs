using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Madame O (the Level 3 boss) has TWO attacks (the older ones below are switched off with Use Old Attacks):
//   A) Chain Bind  - a red mark follows you, then locks; chains burst out of the ground there. Caught = damage and
//                    you can't move for Grab Hold Time (3 s), and Oscar vomits at you. Dash away to dodge.
//   B) Chain Throw - she throws chains straight at you (3 at once in phase 2).
// The Level 3 boss: the Chain Siren, half woman, half octopus. She never moves: she sits on her throne
// on the left side of the arena and attacks with iron chains. Every attack is telegraphed (red warning lines).
//   1) Low Lash   - a chain sweeps along the floor across the whole arena: JUMP over it.
//   2) High Lash  - a chain sweeps at head height + 1: stay on the ground (don't jump into it).
//   3) Chain Rain - chains slam down from the ceiling where the red marks are.
//   4) Chain Grab - a chain shoots straight at you. Hit = damage, you get yanked toward her, then you can't
//                   move for Grab Hold Time (3 s). If the lizard is in the fight, he vomits right at you. Dash to dodge it.
// Beating her unlocks Siren's Chain for the player (PlayerChain).
//   5) Anchor     - she throws an anchor that lands on you with a shockwave.
// Phase 2 (below half health): everything is faster, lashes come in pairs, more chains rain, two grabs in a row.
// Touching her tentacles hurts. If the player dies she heals fully and the fight starts over.
// She can fight together with the lizard (PoisonBoss): they take turns attacking (BossTurns).
[RequireComponent(typeof(Health))]
public partial class OctopusBoss : MonoBehaviour, IChainStunnable
{
    [Header("Arena")]
    [SerializeField] float groundY = 0f;      // top of the floor
    [SerializeField] float ceilingY = 18f;    // chain rain comes from here
    [SerializeField] float arenaMaxX = 33f;   // right wall: lashes reach this far
    [SerializeField] float rainMinX = 9f;     // chain rain lands between these
    [SerializeField] float rainMaxX = 32f;
    [SerializeField] Transform hands;         // where her chains come from

    [Header("Position")]
    [SerializeField] bool immovable = true; // she never moves: punches, the hook or chains can't push her off her throne

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
    [SerializeField] float grabPullTime = 0.3f; // the yank lasts this long, then you are held in place
    [SerializeField] float grabHoldTime = 3f;   // how long you can't move
    [SerializeField] bool lizardVomitsOnGrab = true; // the lizard vomits at you while you are held

    [Header("Madame O's two attacks")]
    [SerializeField] bool useOldAttacks = false;   // ON = also lashes, chain rain, grab and anchor
    [SerializeField] float bindWarnTime = 0.9f;    // the mark follows you this long...
    [SerializeField] float bindLockTime = 0.35f;   // ...then stays put this long before the chains burst out
    [SerializeField] float bindRadius = 1.6f;      // how close to the mark you get caught
    [SerializeField] int bindDamage = 12;
    [SerializeField] float bindFps = 12f;
    [SerializeField] int bindHitFrame = 3;          // frame of the bind animation where the chains catch you
    [SerializeField] int throwChains = 1;           // chains per throw (phase 2: 3)
    [SerializeField] float throwSpread = 14f;       // angle between chains
    [SerializeField] float throwSpeed = 18f;
    [SerializeField] int throwDamage = 12;
    [SerializeField] float throwWarnTime = 0.45f;

    [Header("Art (filled by Tools > Art > Madame O)")]
    [SerializeField] SpriteRenderer artRenderer;    // child that shows her (the body box stays for hits)
    [SerializeField] bool artFacesRight = false;    // her drawing throws to the left
    [SerializeField] Sprite[] idleFrames;
    [SerializeField] float idleFps = 8f;
    [SerializeField] Sprite[] attackFrames;
    [SerializeField] float attackFps = 14f;
    [SerializeField] Sprite[] bindFrames;           // chains bursting out of the ground (pivot at the bottom)
    [SerializeField] Sprite chainSprite;            // one thrown chain (drawn standing up)

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

    float Speed => (phase2 ? phase2SpeedUp : 1f) * SlowFactor;
    // Player's Siren's Chain: stunned, then slower for a while (see ChainStun)
    float slowUntil, slowFactor = 1f, resumeDelay = -1f;
    float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;

    Vector2 HandPos => hands ? (Vector2)hands.position : (Vector2)transform.position;

    Vector3 homePosition; // where she sits

    void Awake()
    {
        homePosition = transform.position;
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

    float animTime, attackPoseUntil;

    // After everything else this frame (punch knockback, hook, chains): put her back on her throne
    void LateUpdate()
    {
        if (immovable && transform.position != homePosition) transform.position = homePosition;
    }

    void Update()
    {
        if (artRenderer && idleFrames != null && idleFrames.Length > 0)
        {
            animTime += Time.deltaTime;
            bool attacking = Time.time < attackPoseUntil && attackFrames != null && attackFrames.Length > 0;
            artRenderer.sprite = attacking
                ? attackFrames[Mathf.Min(attackFrames.Length - 1, Mathf.FloorToInt(animTime * attackFps))]
                : idleFrames[Mathf.FloorToInt(animTime * idleFps) % idleFrames.Length];
            if (player) artRenderer.flipX = (player.position.x > transform.position.x) == artFacesRight ? false : true;
        }
        if (!phase2 && health.CurrentHealth > 0 && health.CurrentHealth <= health.MaxHealth * phase2At)
        {
            phase2 = true;
            if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.5f, 0.3f);
        }
    }

    // ---------------- Fight loop ----------------

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(resumeDelay >= 0f ? resumeDelay : startDelay); // after a chain stun: only the stun
        resumeDelay = -1f;
        while (true)
        {
            while (DialogueManager.Busy) yield return null; // people are talking: no attacks
            if (!player) { yield return null; continue; }

            // Fighting together with another boss: wait (up to Max Turn Wait) while it attacks
            for (float w = 0f; w < maxTurnWait && BossTurns.OtherIsAttacking(this); w += Time.deltaTime) yield return null;
            BossTurns.Begin(this);

            int attack;
            if (useOldAttacks) { do attack = Random.Range(0, 7); while (attack == lastAttack); }
            else { do attack = Random.Range(5, 7); while (attack == lastAttack && Random.value < 0.6f); } // mostly alternates
            lastAttack = attack;

            switch (attack)
            {
                case 5: yield return Bind(); break;
                case 6: yield return Throw(); break;
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

    // ---------------- Reset and death ----------------

    // The player's Siren's Chain hit him: he stops what he is doing for stunTime, then is slower for slowTime
    public void ChainStun(float stunTime, float slowMultiplier, float slowTime)
    {
        if (!health || health.CurrentHealth <= 0) return;
        slowFactor = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
        slowUntil = Time.time + stunTime + slowTime;
        StopAllCoroutines();
        BossTurns.End(this);
        ClearSpawned();
        FlashParts(false);
        resumeDelay = stunTime;
        StartCoroutine(FightLoop());
    }

    void ResetFight()
    {
        if (player && player.TryGetComponent(out PlayerController2D pc)) pc.Unroot();
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
        PlayerChain.UnlockSirenChain(); // the player gets her chain
        if (player && player.TryGetComponent(out PlayerController2D pc)) pc.Unroot();
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
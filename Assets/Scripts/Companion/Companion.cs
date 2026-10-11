using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// The companion you free from the jail in Level 2. Once freed she stays with you for the rest of the game
// (saved, every scene). If you never find her, she is gone for good.
//  - She floats behind you in calm places.
//  - When enemies, obstacles or a boss are near, she fades away into sparkles, and comes back when it is safe.
//  - Press Companion (R / RS click by default, changeable in Controls): she appears and calls down a column of
//    light on the nearest enemy in front of you. It hurts every enemy and boss inside it.
//    Then she fades away again if it is still dangerous.
// All the numbers are in Assets/Resources/CompanionSettings.asset. Nothing needs to be set up in scenes.
public partial class Companion : MonoBehaviour
{
    public const string FoundSave = "companion.found";
    public static bool Found => PlayerPrefs.GetInt(FoundSave, 0) == 1;

    // Freed from the jail: she joins you from now on. Pass where she is standing so she starts there.
    public static void Join(Vector3 at)
    {
        PlayerPrefs.SetInt(FoundSave, 1);
        PlayerPrefs.SetInt(FoundSave + ".new", 1);
        PlayerPrefs.Save();
        Spawn(at);
    }

    // Keep her out (not hiding) for at least this long, e.g. while she is talking
    public static void StayFor(float seconds)
    {
        Companion c = FindAnyObjectByType<Companion>();
        if (c) c.stayUntil = Time.time + seconds;
    }

    // Testing: forget her
    public static void Leave()
    {
        PlayerPrefs.DeleteKey(FoundSave);
        PlayerPrefs.DeleteKey(FoundSave + ".new");
        PlayerPrefs.Save();
        Companion c = FindAnyObjectByType<Companion>();
        if (c) Destroy(c.gameObject);
    }

    // ---------------- Appears in every scene once found ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (Found) Spawn(null);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Found) Spawn(null);
    }

    static void Spawn(Vector3? at)
    {
        if (FindAnyObjectByType<Companion>()) return;
        if (SceneManager.GetActiveScene().name.StartsWith("Tutorial")) return; // never in the tutorial
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (!pc) return;
        var go = new GameObject("Companion");
        go.transform.position = at ?? pc.transform.position + new Vector3(-1.5f, 0.5f, 0f);
        go.AddComponent<Companion>();
    }

    // ---------------- Setup ----------------

    CompanionSettings S => CompanionSettings.Current;
    bool HasArt => S.idleFrames != null && S.idleFrames.Length > 0;
    bool Drawn => HasArt || S.sprite;

    PlayerController2D player;
    Collider2D playerCol;
    SpriteRenderer body;
    Light2D glow;
    Vector3 velocity;
    float visible = 1f;       // 1 = fully shown, 0 = gone
    bool hidden;
    bool attacking;
    float safeTimer;
    float checkTimer;
    float cooldownLeft;
    float idleTime, appearTime = 99f;
    float stayUntil; // no hiding before this time

    // Found once per scene instead of searching every script 4 times a second
    Health[] bossHealth = new Health[0];
    // Reused by the danger check, so it creates no garbage
    static readonly List<Collider2D> nearby = new List<Collider2D>();
    static readonly List<RaycastHit2D> lineHits = new List<RaycastHit2D>();
    static ContactFilter2D nearbyFilter, groundFilter;
    static int groundMask;

    GameObject iconRoot;
    Image cooldownSweep;
    TMP_Text keyText, newText;

    void Awake()
    {
        if (RunOrder.InTutorial) { Destroy(gameObject); return; } // never in the tutorial rooms
        player = FindAnyObjectByType<PlayerController2D>();
        if (player) playerCol = player.GetComponent<Collider2D>();

        var look = new GameObject("Body");
        look.transform.SetParent(transform, false);
        body = look.AddComponent<SpriteRenderer>();
        body.sprite = HasArt ? S.idleFrames[0] : S.sprite ? S.sprite : WhiteSprite;
        body.color = Drawn ? Color.white : S.color;
        body.sortingOrder = 4;
        if (!Drawn) look.transform.localScale = new Vector3(S.size.x, S.size.y, 1f);
        appearTime = 0f; // pops in with the appearing animation
        stayUntil = Time.time + S.joinStayTime;

        glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = S.glowColor;
        glow.intensity = 0.8f;
        glow.pointLightOuterRadius = 3.5f;
        glow.pointLightInnerRadius = 0.3f;

        BuildIcon();

        FindBosses();

        // A boss room: she starts hidden (no free 5 seconds out)
        if (AnyBossStanding())
        {
            hidden = true;
            visible = 0f;
            stayUntil = 0f;
        }
    }

    // Remembers every boss in this scene (anything the Siren's Chain can stun) and sets up the physics filters.
    // Bosses are never spawned during play, so once per scene is enough.
    void FindBosses()
    {
        var list = new List<Health>();
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb is IChainStunnable && mb.TryGetComponent(out Health h)) list.Add(h);
        bossHealth = list.ToArray();

        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground");
        // Same results as before: OverlapCircleAll / LinecastAll used these layers and the project's trigger setting
        nearbyFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        nearbyFilter.useTriggers = Physics2D.queriesHitTriggers;
        groundFilter.SetLayerMask(groundMask);
        groundFilter.useTriggers = Physics2D.queriesHitTriggers;
    }

    // Is any boss in this scene still standing? (Beaten or removed bosses don't count.)
    bool AnyBossStanding()
    {
        foreach (Health h in bossHealth)
            if (h && h.gameObject.activeInHierarchy && h.CurrentHealth > 0) return true;
        return false;
    }

    void OnDestroy()
    {
        if (iconRoot) Destroy(iconRoot);
        if (cursor) Destroy(cursor.gameObject);
        IsAiming = false;
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        if (!body) return; // removed (tutorial room)
        if (!player) { player = FindAnyObjectByType<PlayerController2D>(); return; }

        Follow();

        // Danger nearby? Check a few times a second.
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f && !attacking)
        {
            checkTimer = 0.25f;
            bool danger = DangerNear();
            if (danger) { safeTimer = 0f; if (!hidden && Time.time >= stayUntil) Hide(true); }
            else if (hidden)
            {
                safeTimer += 0.25f;
                if (safeTimer >= S.safeTimeToReturn) Hide(false);
            }
        }

        // Fade in / out
        float target = hidden && !attacking ? 0f : 1f;
        visible = Mathf.MoveTowards(visible, target, Time.deltaTime / Mathf.Max(S.fadeTime, 0.01f));
        Color c = body.color; c.a = visible; body.color = c;
        body.transform.localScale = Drawn ? Vector3.one * S.drawScale * Mathf.Lerp(0.6f, 1f, visible)
                                          : new Vector3(S.size.x, S.size.y * Mathf.Lerp(0.3f, 1f, visible), 1f);
        if (HasArt) Animate();
        glow.intensity = 0.8f * visible;

        // Light beam
        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        UpdateIcon();
        if (Time.timeScale == 0f || attacking || cooldownLeft > 0f) return;
        if (!player.enabled || player.IsRooted) return;
        if (GameInput.CompanionPressed) StartCoroutine(LightBeam());
    }

    void Follow()
    {
        float facing = player.FacingDirection;
        Vector3 target = player.transform.position + new Vector3(-facing * S.followDistance, S.followHeight, 0f);
        target.y += Mathf.Sin(Time.time * S.bobSpeed) * S.bobHeight;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, S.followSmoothTime);
        if (Drawn) body.flipX = facing < 0f;
    }

    // Idle float, or the appearing animation right after she pops in
    void Animate()
    {
        appearTime += Time.deltaTime;
        int appearCount = S.appearFrames != null ? S.appearFrames.Length : 0;
        int a = Mathf.FloorToInt(appearTime * Mathf.Max(S.appearFps, 0.01f));
        if (a < appearCount) { body.sprite = S.appearFrames[a]; return; }
        idleTime += Time.deltaTime;
        body.sprite = S.idleFrames[Mathf.FloorToInt(idleTime * Mathf.Max(S.idleFps, 0.01f)) % S.idleFrames.Length];
    }

    // Enemies, obstacles and bosses within the danger radius (pits and walls don't count)
    bool DangerNear()
    {
        // A boss fight: she always stays hidden while any boss is still standing
        if (AnyBossStanding()) return true;

        Vector2 me = player.transform.position;
        Physics2D.OverlapCircle(me, S.dangerRadius, nearbyFilter, nearby); // fills the reused list
        foreach (Collider2D hit in nearby)
        {
            if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(transform)) continue;
            if (S.ignoreDangerBehindWalls && BehindWall(me, hit)) continue;
            if (hit.GetComponentInParent<IChainStunnable>() != null) return true; // a boss
            if (hit.GetComponentInParent<PoisonHazard>() || hit.GetComponentInParent<DamageZone>()
                || hit.GetComponentInParent<ContactDamage>() || hit.GetComponentInParent<EnemyChase>()
                || hit.GetComponentInParent<EnemyShooter>() || hit.GetComponentInParent<ChainTrap>()
                || hit.GetComponentInParent<ExplodingBarrel>() || hit.GetComponentInParent<PoisonPendulum>())
                return true;
            Health h = hit.GetComponentInParent<Health>();
            if (h && h.CurrentHealth > 0 && !h.GetComponent<PlayerController2D>() && !h.GetComponent<BreakableWall>()) return true;
        }
        return false;
    }

    // Is there a wall or floor between the player and this thing? (Its own collider doesn't count.)
    static bool BehindWall(Vector2 from, Collider2D target)
    {
        Physics2D.Linecast(from, target.bounds.center, groundFilter, lineHits); // fills the reused list
        foreach (RaycastHit2D h in lineHits)
            if (h.collider != target && !h.collider.transform.IsChildOf(target.transform.root)) return true;
        return false;
    }

    // Fades away (or back) with a burst of sparkles
    void Hide(bool hide)
    {
        if (hidden == hide) return;
        hidden = hide;
        if (!hide) { appearTime = 0f; stayUntil = Time.time + S.minShowTime; }
        StartCoroutine(Sparkles(12));
    }

    IEnumerator Sparkles(int count)
    {
        var bits = new Transform[count];
        var dirs = new Vector2[count];
        var srs = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Sparkle");
            go.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);
            go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
            srs[i] = go.AddComponent<SpriteRenderer>();
            srs[i].sprite = WhiteSprite;
            srs[i].color = S.glowColor;
            srs[i].sortingOrder = 6;
            bits[i] = go.transform;
            dirs[i] = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 2f));
        }
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            for (int i = 0; i < count; i++)
            {
                if (!bits[i]) continue;
                bits[i].position += (Vector3)(dirs[i] * Time.deltaTime);
                Color c = S.glowColor; c.a = 1f - t / 0.7f; srs[i].color = c;
            }
            yield return null;
        }
        foreach (Transform b in bits) if (b) Destroy(b.gameObject);
    }
}
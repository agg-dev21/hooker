using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// Madame O's Chain: the player's second special ability, unlocked by beating Madame O (the Level 3 octopus boss).
// Press Chain Ability (C / View button by default, can be changed in the pause menu Controls):
// a chain shoots forward. The first enemy it hits takes damage, gets pulled to you, and can't move
// for Hold Time (3 s), just like her Chain Grab does to you. Bosses can't be pulled: they take the damage,
// are stunned for Boss Stun Time (1 s), then are slower for Boss Slow Time.
//  - An icon left of the Hardy's Spikes icon shows the key and a dark sweep while it recharges.
//  - The unlock is saved. From Level 4 on it is always unlocked (for testing from a Level 4 scene).
// Nothing needs to be set up in scenes: this adds itself to the player in every scene.
public class PlayerChain : MonoBehaviour
{
    public const string SirenChainSave = "ability.sirenChain";
    const string SirenChainNewSave = SirenChainSave + ".new"; // built once, not every frame
    public static bool SirenChainUnlocked => !RunOrder.InTutorial && PlayerPrefs.GetInt(SirenChainSave, 0) == 1; // never in the tutorial

    // Called when the Siren dies
    public static void UnlockSirenChain()
    {
        if (SirenChainUnlocked) return;
        PlayerPrefs.SetInt(SirenChainSave, 1);
        PlayerPrefs.SetInt(SirenChainNewSave, 1); // "NEW!" on the icon until it is used once
        PlayerPrefs.Save();
        PlayerItems.Toast(TutorialSign.Fill("New power: MADAME O'S CHAIN!\nPress {ChainAbility} to pull an enemy to you and hold it still."), 6f);
    }

    [Header("Madame O's Chain")]
    [SerializeField] float range = 14f;
    [SerializeField] float chainSpeed = 40f;
    [SerializeField] float hitRadius = 0.6f;
    [SerializeField] float heightAboveFeet = 1.2f;
    [SerializeField] int damage = 10;
    [SerializeField] float pullSpeed = 18f;     // how fast the enemy is pulled to you
    [SerializeField] float pullStopDistance = 1.5f; // stops this far in front of you
    [SerializeField] float maxPullTime = 0.5f;
    [SerializeField] float holdTime = 3f;       // how long the enemy can't move
    [SerializeField] float bossStunTime = 1f;   // bosses: stunned this long
    [SerializeField, Range(0.05f, 1f)] float bossSlowMultiplier = 0.5f; // then this much slower (0.5 = half speed)
    [SerializeField] float bossSlowTime = 3f;   // for this long
    [SerializeField] float cooldown = 8f;
    [SerializeField] Color chainColor = new Color(0.6f, 0.6f, 0.68f);

    [Header("Icon (bottom right, left of Hardy's Spikes)")]
    [SerializeField] float iconSize = 100f;
    [SerializeField] Vector2 iconMargin = new Vector2(210f, 80f);

    PlayerController2D controller;
    Collider2D playerCol;
    float cooldownLeft;
    bool casting;

    GameObject iconRoot;
    Image cooldownSweep;
    TMP_Text keyText, newText;

    // ---------------- Adds itself to the player in every scene ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToPlayer();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToPlayer();

    static void AddToPlayer()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene.StartsWith("Level_4") || scene.StartsWith("Level_5")) UnlockSirenChain(); // the Siren is beaten by Level 4
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerChain>()) pc.gameObject.AddComponent<PlayerChain>();
    }

    // ---------------- Every frame ----------------

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        playerCol = GetComponent<Collider2D>();
        BuildIcon();
    }

    void OnDestroy()
    {
        if (iconRoot) Destroy(iconRoot);
    }

    void Update()
    {
        bool unlocked = SirenChainUnlocked;
        if (iconRoot.activeSelf != unlocked) iconRoot.SetActive(unlocked);
        if (!unlocked) return;

        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        cooldownSweep.fillAmount = Mathf.Clamp01(cooldownLeft / cooldown);
        keyText.text = GameInput.KeyName(GameInput.GetKey(GameAction.ChainAbility));
        newText.gameObject.SetActive(PlayerPrefs.GetInt(SirenChainNewSave, 0) == 1);

        if (Time.timeScale == 0f || casting || cooldownLeft > 0f) return;
        if (controller && (!controller.enabled || controller.IsRooted)) return; // hooked, on the boat, or chained yourself
        if (GameInput.ChainAbilityPressed) StartCoroutine(Cast());
    }

    // ---------------- The chain ----------------

    IEnumerator Cast()
    {
        casting = true;
        cooldownLeft = cooldown;
        PlayerPrefs.SetInt(SirenChainNewSave, 0);

        float dir = controller ? controller.FacingDirection : 1f;
        LineRenderer line = MakeLine();
        Health target = null;
        float traveled = 0f;
        int ground = LayerMask.GetMask("Ground");

        // Shoot forward until it hits an enemy, a wall, or runs out of range
        while (traveled < range)
        {
            traveled = Mathf.MoveTowards(traveled, range, chainSpeed * Time.deltaTime);
            Vector2 from = HandPos(dir);
            Vector2 tip = from + Vector2.right * dir * traveled;
            line.SetPosition(0, from);
            line.SetPosition(1, tip);
            target = FindTarget(tip);
            if (target || Physics2D.OverlapPoint(tip, ground)) break;
            yield return null;
        }

        if (target)
        {
            target.TakeDamage(damage);
            IChainStunnable boss = target ? target.GetComponentInParent<IChainStunnable>() : null;
            if (boss != null) boss.ChainStun(bossStunTime, bossSlowMultiplier, bossSlowTime);
            else if (target && !ChainHold.IsBoss(target.gameObject))
            {
                // Pull it to you, then hold it
                Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
                for (float t = 0f; t < maxPullTime && target; t += Time.deltaTime)
                {
                    Vector2 me = HandPos(dir);
                    Vector2 pos = rb ? rb.position : (Vector2)target.transform.position;
                    line.SetPosition(0, me);
                    line.SetPosition(1, pos);
                    if (Mathf.Abs(pos.x - me.x) <= pullStopDistance) break;
                    Vector2 next = Vector2.MoveTowards(pos, new Vector2(me.x + dir * pullStopDistance, pos.y), pullSpeed * Time.deltaTime);
                    if (rb && rb.bodyType != RigidbodyType2D.Static) rb.MovePosition(next);
                    else target.transform.position = next;
                    yield return null;
                }
                if (target) ChainHold.Apply(target.gameObject, holdTime);
            }
        }

        // Pull the chain back in
        Vector2 end = line.GetPosition(1);
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            Vector2 from = HandPos(dir);
            line.SetPosition(0, from);
            line.SetPosition(1, Vector2.Lerp(end, from, t / 0.15f));
            yield return null;
        }
        Destroy(line.gameObject);
        casting = false;
    }

    Vector2 HandPos(float dir)
    {
        float feet = playerCol ? playerCol.bounds.min.y : transform.position.y - 1f;
        float halfWidth = playerCol ? playerCol.bounds.extents.x : 0.5f;
        return new Vector2(transform.position.x + dir * halfWidth, feet + heightAboveFeet);
    }

    // First thing with Health at the chain's tip that isn't the player
    Health FindTarget(Vector2 tip)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(tip, hitRadius))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h && h.CurrentHealth > 0 && !h.GetComponent<PlayerController2D>()) return h;
        }
        return null;
    }

    LineRenderer MakeLine()
    {
        var go = new GameObject("PlayerChain");
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = line.endWidth = 0.15f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = chainColor;
        line.sortingOrder = 15;
        line.useWorldSpace = true;
        return line;
    }

    // ---------------- Icon ----------------

    void BuildIcon()
    {
        iconRoot = new GameObject("ChainAbilityIcon");
        var canvas = iconRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = iconRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform frame = Panel("Frame", iconRoot.transform, new Color(0.06f, 0.07f, 0.12f, 0.85f));
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-iconMargin.x, iconMargin.y);
        frame.sizeDelta = new Vector2(iconSize, iconSize);

        RectTransform border = Panel("Border", frame, new Color(0.55f, 0.6f, 0.75f));
        Fill(border, -4f);
        border.SetAsFirstSibling();

        // Placeholder picture: a few chain links
        for (int i = 0; i < 4; i++)
        {
            RectTransform link = Panel("Link", frame, i % 2 == 0 ? new Color(0.7f, 0.72f, 0.8f) : new Color(0.45f, 0.47f, 0.55f));
            link.anchorMin = link.anchorMax = new Vector2(0.5f, 0.5f);
            link.sizeDelta = new Vector2(iconSize * 0.22f, iconSize * 0.14f);
            link.anchoredPosition = new Vector2((i - 1.5f) * iconSize * 0.18f, (i - 1.5f) * iconSize * 0.12f);
            link.localRotation = Quaternion.Euler(0f, 0f, 35f);
        }

        RectTransform sweepRt = Panel("Cooldown", frame, new Color(0f, 0f, 0f, 0.7f));
        Fill(sweepRt, 0f);
        cooldownSweep = sweepRt.GetComponent<Image>();
        cooldownSweep.type = Image.Type.Filled;
        cooldownSweep.fillMethod = Image.FillMethod.Radial360;
        cooldownSweep.fillOrigin = (int)Image.Origin360.Top;
        cooldownSweep.fillClockwise = false;
        cooldownSweep.fillAmount = 0f;

        keyText = Label("Key", frame, "C", 28f, new Vector2(0f, -30f));
        newText = Label("New", frame, "NEW!", 24f, new Vector2(0f, iconSize * 0.5f + 20f));
        newText.color = new Color(1f, 0.85f, 0.2f);

        iconRoot.SetActive(false);
    }
}
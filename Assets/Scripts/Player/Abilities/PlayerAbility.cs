using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// The player's special ability, unlocked by beating a boss (like Genshin's elemental skill).
// Right now: Hardy's Spikes - beat Hardy and you can shoot his sideways spike attack (it sets enemies on fire).
// From Level 3 on it is always unlocked (Levels 1 and 2 come in a random order, so it is earned there).
//  - Press the Ability button (Q / LB by default, can be changed in the pause menu Controls).
//  - An icon in the bottom-right corner shows the key and a dark sweep while it recharges.
//  - The unlock is saved, so it stays after quitting.
// Nothing needs to be set up in scenes: this adds itself to the player in every scene.
// The pictures come from Assets/Resources/AbilityArt.asset (made by Tools > Art > Hardy).
public class PlayerAbility : MonoBehaviour
{
    public const string HardySpikesSave = "ability.hardySpikes";
    public static bool HardySpikesUnlocked => !RunOrder.InTutorial && PlayerPrefs.GetInt(HardySpikesSave, 0) == 1; // never in the tutorial

    // Called when Hardy dies
    public static void UnlockHardySpikes()
    {
        if (HardySpikesUnlocked) return;
        PlayerPrefs.SetInt(HardySpikesSave, 1);
        PlayerPrefs.SetInt(HardySpikesSave + ".new", 1); // show "NEW!" on the icon until it is used once
        PlayerPrefs.Save();
        PlayerItems.Toast(TutorialSign.Fill("New power: HARDY'S SPIKES!\nPress {Ability} to shoot his spikes. They set enemies on fire."), 6f);
    }

    // Hardy's Spikes works exactly like Hardy's horizontal attack (Tentacle Lash): the same animation,
    // stretched to Length, a body hit and a stronger tip hit, only during the hit frames, and his fire.
    [Header("Hardy's Spikes (same as Hardy's Tentacle Lash)")]
    [SerializeField] float length = 18f;          // Hardy: Tentacle Length
    [SerializeField] float tipLength = 2f;        // Hardy: Tentacle Tip Length (the last part hits harder)
    [SerializeField] float thickness = 1.6f;      // Hardy: Tentacle Thickness (hit area height; the tip is 1.2x)
    [SerializeField] float heightAboveFeet = 1f;  // Hardy: Tentacle Height
    [SerializeField] float drawnThickness = 1f;   // Hardy: Tentacle Fx Thickness Scale
    [SerializeField] float fps = 18f;             // Hardy: Tentacle Fx Fps
    [SerializeField] int hitFirstFrame = 6;       // Hardy: Tentacle Hit First Frame
    [SerializeField] int hitLastFrame = 11;       // Hardy: Tentacle Hit Last Frame
    [SerializeField] int bodyDamage = 20;         // to enemies (Hardy's own numbers are for hitting the player)
    [SerializeField] int tipDamage = 30;
    [SerializeField] float knockback = 10f;       // Hardy: Tentacle Knockback
    [SerializeField] bool burns = true;           // Hardy: Tentacle Burns
    [SerializeField] int burnDamage = 2;          // Hardy: Burn Damage
    [SerializeField] float burnDuration = 3f;     // Hardy: Burn Duration
    [SerializeField] float burnTickInterval = 1f; // Hardy: Burn Tick Interval
    [SerializeField] float cooldown = 6f;

    [Header("Icon (bottom right)")]
    [SerializeField] float iconSize = 120f;
    [SerializeField] Vector2 iconMargin = new Vector2(60f, 70f);

    AbilityArt art;
    PlayerController2D controller;
    Collider2D playerCol;
    float cooldownLeft;
    bool casting;

    GameObject iconRoot;
    Image cooldownSweep;
    TMP_Text keyText;
    TMP_Text newText;

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
        int level = LevelNumber(SceneManager.GetActiveScene().name);
        if (level >= 3) UnlockHardySpikes();               // Hardy is beaten by Level 3 (Levels 1 and 2 come in a random order)
        if (level >= 3) PlayerUpgrades.UnlockPoisonHook(); // the lizard is beaten by Level 3
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerAbility>()) pc.gameObject.AddComponent<PlayerAbility>();
    }

    // "Level_2_Boss" -> 2, "Tutorial_3_Dash" -> 0 (not a level)
    static int LevelNumber(string sceneName)
    {
        if (!sceneName.StartsWith("Level_")) return 0;
        string rest = sceneName.Substring(6);
        int end = 0;
        while (end < rest.Length && char.IsDigit(rest[end])) end++;
        return end > 0 && int.TryParse(rest.Substring(0, end), out int n) ? n : 0;
    }

    // ---------------- Setup ----------------

    void Awake()
    {
        art = Resources.Load<AbilityArt>("AbilityArt");
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
        bool unlocked = HardySpikesUnlocked;
        if (iconRoot.activeSelf != unlocked) iconRoot.SetActive(unlocked);
        if (!unlocked) return;

        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        cooldownSweep.fillAmount = Mathf.Clamp01(cooldownLeft / cooldown);
        keyText.text = GameInput.KeyName(GameInput.GetKey(GameAction.Ability));
        newText.gameObject.SetActive(PlayerPrefs.GetInt(HardySpikesSave + ".new", 0) == 1);

        if (Time.timeScale == 0f || casting || cooldownLeft > 0f) return;
        if (controller && !controller.enabled) return; // being pulled by the hook, riding the boat...
        if (GameInput.AbilityPressed) StartCoroutine(HardySpikes());
    }

    // ---------------- Hardy's Spikes ----------------

    IEnumerator HardySpikes()
    {
        casting = true;
        cooldownLeft = cooldown;
        PlayerPrefs.SetInt(HardySpikesSave + ".new", 0);

        float dir = controller ? controller.FacingDirection : 1f;
        float feet = playerCol ? playerCol.bounds.min.y : transform.position.y - 1f;
        float halfWidth = playerCol ? playerCol.bounds.extents.x : 0.5f;
        Vector2 start = new Vector2(transform.position.x + dir * halfWidth, feet + heightAboveFeet);

        // The drawing: Hardy's horizontal attack, stretched to Length, flipped to face forward (the art grows to the left)
        GameObject fx = null;
        SpriteRenderer fxSr = null;
        Sprite[] frames = art ? art.hardySpikesFrames : null;
        bool hasArt = frames != null && frames.Length > 0;
        if (hasArt)
        {
            fx = new GameObject("PlayerSpikes");
            fx.transform.position = start;
            fxSr = fx.AddComponent<SpriteRenderer>();
            fxSr.sortingOrder = 15;
            fxSr.sprite = frames[0];
            float drawn = Mathf.Max(fxSr.sprite.bounds.size.x, 0.01f);
            fx.transform.localScale = new Vector3(-dir * length / drawn, drawnThickness, 1f);
        }

        // Body and tip hit areas, like Hardy's (the tip is the far end and hits harder)
        float tip = Mathf.Min(tipLength, length);
        Vector2 bodyCenter = start + Vector2.right * dir * (length - tip) * 0.5f;
        Vector2 bodySize = new Vector2(Mathf.Max(length - tip, 0.01f), thickness);
        Vector2 tipCenter = start + Vector2.right * dir * (length - tip * 0.5f);
        Vector2 tipSize = new Vector2(tip, thickness * 1.2f);

        var alreadyHit = new HashSet<Health>(); // each enemy is hit once per cast
        int frameCount = hasArt ? frames.Length : hitLastFrame + 1;
        float frameTime = 1f / Mathf.Max(fps, 0.01f);
        for (int i = 0; i < frameCount; i++)
        {
            if (fxSr) fxSr.sprite = frames[i];
            if (i >= hitFirstFrame && i <= hitLastFrame)
            {
                HitArea(tipCenter, tipSize, tipDamage, dir, alreadyHit);   // tip first, so it wins over the body
                HitArea(bodyCenter, bodySize, bodyDamage, dir, alreadyHit);
            }
            yield return new WaitForSeconds(frameTime);
        }

        if (fx) Destroy(fx);
        casting = false;
    }

    void HitArea(Vector2 center, Vector2 size, int damage, float dir, HashSet<Health> alreadyHit)
    {
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (hit.transform.IsChildOf(transform)) continue; // not the player
            Health h = hit.GetComponentInParent<Health>();
            if (!h || alreadyHit.Contains(h)) continue;
            alreadyHit.Add(h);
            h.TakeDamage(damage);
            if (burns) Burn.Apply(h.gameObject, burnDamage, burnDuration, burnTickInterval); // Hardy's fire
            Rigidbody2D rb = hit.attachedRigidbody;
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(dir, 0.5f).normalized * knockback;
        }
    }

    // ---------------- Icon ----------------

    // A framed square in the bottom-right corner: the ability picture, a dark sweep while recharging, and the key under it
    void BuildIcon()
    {
        iconRoot = new GameObject("AbilityIcon");
        var canvas = iconRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = iconRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform frame = Panel("Frame", iconRoot.transform, new Color(0.08f, 0.05f, 0.12f, 0.85f));
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-iconMargin.x, iconMargin.y);
        frame.sizeDelta = new Vector2(iconSize, iconSize);

        RectTransform border = Panel("Border", frame, new Color(0.55f, 0.25f, 0.9f));
        Fill(border, -4f);
        border.SetAsFirstSibling();

        // The ability picture
        var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        picGo.transform.SetParent(frame, false);
        Fill((RectTransform)picGo.transform, 10f);
        var pic = picGo.GetComponent<Image>();
        pic.sprite = art && art.hardySpikesIcon ? art.hardySpikesIcon : WhiteSprite;
        pic.preserveAspect = true;
        if (!art || !art.hardySpikesIcon) pic.color = new Color(0.5f, 0.2f, 0.85f);

        // Dark sweep that shrinks as it recharges
        RectTransform sweepRt = Panel("Cooldown", frame, new Color(0f, 0f, 0f, 0.7f));
        Fill(sweepRt, 0f);
        cooldownSweep = sweepRt.GetComponent<Image>();
        cooldownSweep.type = Image.Type.Filled;
        cooldownSweep.fillMethod = Image.FillMethod.Radial360;
        cooldownSweep.fillOrigin = (int)Image.Origin360.Top;
        cooldownSweep.fillClockwise = false;
        cooldownSweep.fillAmount = 0f;

        keyText = Label("Key", frame, "Q", 30f, new Vector2(0f, -32f));
        newText = Label("New", frame, "NEW!", 26f, new Vector2(0f, iconSize * 0.5f + 22f));
        newText.color = new Color(1f, 0.85f, 0.2f);

        iconRoot.SetActive(false);
    }
}

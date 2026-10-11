using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The player's inventory in action: the item bar at the bottom of the screen, using items, and their effects.
//   Next Item (Tab / LT) opens the inventory: the game pauses. Then Tab picks the next item,
//   Use Item (F / RT) uses it and closes, Interact (E) or Pause (Esc) closes without using.
//   With the inventory closed, 1-9 picks an item and F uses it right away (no pause).
//   Items are kept between levels and between play sessions (see Inventory).
//   Shield            soaks up damage until it breaks or runs out of time
//   Poison Resistance poison hurts much less for a while (and cures current poison)
//   Healing           heals
//   Instant Revival   used automatically when you die: you get back up on the spot
//   Flying            for a while, hold Jump to rise and let go to float down
//   Scroll            opens the note to read (the game pauses); it stays in the inventory
//   Speed Boost       faster movement and faster attacks for a while
//   Invulnerability   nothing can hurt you for a few seconds
// All the numbers are in Assets/Resources/ItemSettings.asset.
// Nothing needs to be set up in scenes: this adds itself to the player in every scene.
[DefaultExecutionOrder(100)] // runs after the player's movement, so Flying can change the vertical speed
public class PlayerItems : MonoBehaviour
{
    static PlayerItems instance;

    Health health;
    Rigidbody2D rb;
    PlayerController2D controller;
    PlayerCombat combat;
    int selected;

    float shieldTime, resistTime, flyTime, speedTime, invulnerableTime;
    int shieldLeft;

    GameObject uiRoot, barRoot, reader, inventoryDim;
    TMP_Text inventoryTitle, hintText, effectsText, toastText, readerTitle, readerBody;
    float toastTimer;
    bool reading;
    bool inventoryOpen;
    static Sprite whiteSprite;

    ItemSettings S => ItemSettings.Current;

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
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerItems>()) pc.gameObject.AddComponent<PlayerItems>();
    }

    // Small message in the middle of the screen ("Got: Shield")
    public static void Toast(string message)
    {
        if (instance) instance.ShowToast(message);
    }

    // Longer message, shown for this many seconds (new powers)
    public static void Toast(string message, float seconds)
    {
        if (instance) instance.ShowToast(message, seconds);
    }

    // ---------------- Setup ----------------

    void Awake()
    {
        instance = this;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController2D>();
        combat = GetComponent<PlayerCombat>();
        if (health)
        {
            health.DamageFilter = FilterDamage;
            health.BeforeDeath = TryRevive;
        }
        BuildUi();
        Inventory.Changed += RefreshBar;
        RefreshBar();
    }

    void OnDestroy()
    {
        Inventory.Changed -= RefreshBar;
        if (instance == this) instance = null;
        if (health)
        {
            health.DamageFilter = null;
            health.BeforeDeath = null;
        }
        if (reading || inventoryOpen) Time.timeScale = 1f;
        if (uiRoot) Destroy(uiRoot);
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        if (reading)
        {
            if (GameInput.Pressed(GameAction.Interact) || GameInput.UseItemPressed) CloseReader();
            return;
        }
        if (inventoryOpen)
        {
            InventoryControls();
            return;
        }

        float dt = Time.deltaTime;
        shieldTime -= dt;
        if (shieldTime <= 0f) shieldLeft = 0;
        resistTime -= dt;
        flyTime -= dt;
        speedTime -= dt;
        invulnerableTime -= dt;

        if (controller) controller.SpeedMultiplier = speedTime > 0f ? S.moveSpeedMultiplier : 1f;
        if (combat) combat.AttackSpeedMultiplier = speedTime > 0f ? S.attackSpeedMultiplier : 1f;

        ShowEffects();
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            toastText.alpha = Mathf.Clamp01(toastTimer / 0.5f);
        }

        if (Time.timeScale == 0f) return; // paused

        int count = Inventory.Entries.Count;
        if (count == 0) return;
        if (selected >= count) { selected = count - 1; RefreshBar(); }

        // 1-9 pick an item directly
        Keyboard kb = Keyboard.current;
        if (kb != null)
            for (int i = 0; i < Mathf.Min(count, 9); i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { selected = i; RefreshBar(); ShowSelectedName(); }

        if (GameInput.NextItemPressed) { OpenInventory(); return; }
        if (GameInput.UseItemPressed) Use(selected);
    }

    // ---------------- Inventory open (game paused) ----------------

    void OpenInventory()
    {
        inventoryOpen = true;
        Time.timeScale = 0f;
        RefreshBar();
        ShowSelectedName();
    }

    // Pops up the name of the picked item (and how many) in the middle of the screen
    void ShowSelectedName()
    {
        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        if (selected < 0 || selected >= list.Count) return;
        Inventory.Entry e = list[selected];
        string itemName = e.type == ItemType.Scroll && !string.IsNullOrEmpty(e.title) ? e.title : S.Look(e.type).name;
        ShowToast(e.count > 1 ? itemName + "  x" + e.count : itemName, 1.5f);
    }

    void CloseInventory()
    {
        inventoryOpen = false;
        Unpause();
        RefreshBar();
    }

    void InventoryControls()
    {
        int count = Inventory.Entries.Count;
        // Esc opens the pause menu: close the inventory under it. E closes. Empty inventory closes too.
        if (count == 0 || GameInput.PausePressed || GameInput.Pressed(GameAction.Interact)) { CloseInventory(); return; }
        if (selected >= count) selected = count - 1;

        Keyboard kb = Keyboard.current;
        if (kb != null)
            for (int i = 0; i < Mathf.Min(count, 9); i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { selected = i; RefreshBar(); ShowSelectedName(); }

        if (GameInput.NextItemPressed) { selected = (selected + 1) % count; RefreshBar(); ShowSelectedName(); }
        if (GameInput.UseItemPressed)
        {
            CloseInventory();
            Use(selected);
        }
    }

    // Back to normal speed, unless the pause menu is open
    static void Unpause()
    {
        if (GameManager.Instance && GameManager.Instance.IsPausedOrWon) return;
        Time.timeScale = 1f;
    }

    // Flying: hold Jump to rise, otherwise fall slowly
    void FixedUpdate()
    {
        if (flyTime <= 0f || !rb || (controller && !controller.enabled)) return;
        Vector2 v = rb.linearVelocity;
        v.y = GameInput.JumpHeld ? S.flyUpSpeed : Mathf.Max(v.y, -S.flyFallSpeed);
        rb.linearVelocity = v;
    }

    // ---------------- Using items ----------------

    void Use(int index)
    {
        Inventory.Entry e = Inventory.Entries[index];
        string itemName = S.Look(e.type).name;
        switch (e.type)
        {
            case ItemType.Shield:
                shieldLeft = S.shieldAmount;
                shieldTime = S.shieldDuration;
                break;
            case ItemType.PoisonResistance:
                resistTime = S.poisonResistanceDuration;
                if (S.curesPoisonOnUse) Poison.Cure(gameObject);
                break;
            case ItemType.Healing:
                if (health && health.CurrentHealth >= health.MaxHealth) { ShowToast("Health is already full"); return; }
                if (health) health.Heal(S.healAmount);
                break;
            case ItemType.InstantRevival:
                ShowToast("Used automatically if you die");
                return;
            case ItemType.Flying:
                flyTime = S.flyDuration;
                break;
            case ItemType.Scroll:
                OpenReader(e.title, e.text);
                return; // scrolls are kept
            case ItemType.SpeedBoost:
                speedTime = S.speedDuration;
                break;
            case ItemType.Invulnerability:
                invulnerableTime = S.invulnerableDuration;
                if (health) health.StartIFrames(S.invulnerableDuration); // invincible and blinking
                break;
        }
        Inventory.RemoveOne(index);
        ShowToast("Used: " + itemName);
    }

    // Shield and poison resistance change incoming damage
    int FilterDamage(int amount, bool isTick)
    {
        if (isTick && resistTime > 0f) amount = Mathf.RoundToInt(amount * (1f - S.poisonResistance)); // ticks are poison
        if (shieldTime > 0f && shieldLeft > 0 && amount > 0)
        {
            int absorbed = Mathf.Min(shieldLeft, amount);
            shieldLeft -= absorbed;
            amount -= absorbed;
            if (shieldLeft <= 0) { shieldTime = 0f; ShowToast("Shield broke!"); }
        }
        return amount;
    }

    // Instant Revival: get back up on the spot instead of dying
    bool TryRevive()
    {
        if (!health || !Inventory.RemoveOne(ItemType.InstantRevival)) return false;
        health.Heal(S.reviveHealth - health.CurrentHealth);
        health.StartIFrames(S.reviveInvulnerable);
        Poison.Cure(gameObject);
        ShowToast("Revived!");
        return true;
    }

    // ---------------- Scroll reader ----------------

    void OpenReader(string title, string text)
    {
        readerTitle.text = string.IsNullOrEmpty(title) ? "A note" : title;
        readerBody.text = text;
        reader.SetActive(true);
        reading = true;
        Time.timeScale = 0f;
    }

    void CloseReader()
    {
        reader.SetActive(false);
        reading = false;
        Unpause();
    }

    // ---------------- UI ----------------

    void ShowToast(string message, float seconds = 2f)
    {
        toastText.text = message;
        toastText.alpha = 1f;
        toastTimer = seconds;
    }

    void ShowEffects()
    {
        string s = "";
        if (shieldTime > 0f && shieldLeft > 0) s += "Shield " + shieldLeft + "  (" + Mathf.CeilToInt(shieldTime) + "s)\n";
        if (resistTime > 0f) s += "Poison resist  (" + Mathf.CeilToInt(resistTime) + "s)\n";
        if (flyTime > 0f) s += "Flying  (" + Mathf.CeilToInt(flyTime) + "s)\n";
        if (speedTime > 0f) s += "Speed boost  (" + Mathf.CeilToInt(speedTime) + "s)\n";
        if (invulnerableTime > 0f) s += "Invulnerable  (" + Mathf.CeilToInt(invulnerableTime) + "s)\n";
        int revives = Inventory.Count(ItemType.InstantRevival);
        if (revives > 0) s += "Revival ready x" + revives;
        effectsText.text = s;
    }

    // Rebuilds the item boxes along the bottom of the screen
    void RefreshBar()
    {
        if (!barRoot) return;
        for (int i = barRoot.transform.childCount - 1; i >= 0; i--) Destroy(barRoot.transform.GetChild(i).gameObject);

        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        hintText.gameObject.SetActive(list.Count > 0);
        string next = GameInput.KeyName(GameInput.GetKey(GameAction.NextItem));
        string use = GameInput.KeyName(GameInput.GetKey(GameAction.UseItem));
        hintText.text = inventoryOpen
            ? "[" + next + "] next    [" + use + "] use    [" + GameInput.KeyName(GameInput.GetKey(GameAction.Interact)) + "] close"
            : "[" + next + "] open inventory    [" + use + "] use";
        if (inventoryDim) inventoryDim.SetActive(inventoryOpen);

        const float size = 90f, gap = 12f;
        float startX = -(list.Count - 1) * (size + gap) * 0.5f;
        for (int i = 0; i < list.Count; i++)
        {
            Inventory.Entry e = list[i];
            ItemLook look = S.Look(e.type);
            RectTransform slot = Panel("Slot", barRoot.transform, i == selected ? new Color(1f, 0.85f, 0.3f) : new Color(0.1f, 0.08f, 0.14f, 0.9f));
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            slot.sizeDelta = new Vector2(size, size);
            slot.anchoredPosition = new Vector2(startX + i * (size + gap), size * 0.5f);

            RectTransform inner = Panel("Inner", slot, new Color(0.1f, 0.08f, 0.14f, 0.95f));
            Fill(inner, 4f);
            RectTransform pic = Panel("Picture", inner, look.color);
            Fill(pic, 14f);
            if (look.icon)
            {
                var img = pic.GetComponent<Image>();
                img.sprite = look.icon;
                img.color = Color.white;
                img.preserveAspect = true;
            }

            Label("Number", slot, (i + 1).ToString(), 20f, new Vector2(-size * 0.5f + 12f, size * 0.5f - 14f));
            if (e.count > 1) Label("Count", slot, "x" + e.count, 22f, new Vector2(size * 0.5f - 18f, -size * 0.5f + 14f));
            string label = e.type == ItemType.Scroll && !string.IsNullOrEmpty(e.title) ? e.title : look.name;
            Label("Name", slot, label, 16f, new Vector2(0f, -size * 0.5f - 14f));
        }
    }

    void BuildUi()
    {
        if (!whiteSprite) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        uiRoot = new GameObject("InventoryUI");
        var canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Shown while the inventory is open: dim screen and a title (made first so it sits behind the bar)
        inventoryDim = new GameObject("InventoryDim", typeof(RectTransform), typeof(Image));
        inventoryDim.transform.SetParent(uiRoot.transform, false);
        Fill((RectTransform)inventoryDim.transform, 0f);
        var dimImage = inventoryDim.GetComponent<Image>();
        dimImage.sprite = whiteSprite;
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = false;
        inventoryTitle = Label("Title", inventoryDim.transform, "INVENTORY  (paused)", 40f, Vector2.zero);
        var trt0 = inventoryTitle.rectTransform;
        trt0.anchorMin = trt0.anchorMax = new Vector2(0.5f, 0f);
        trt0.anchoredPosition = new Vector2(0f, 230f);
        trt0.sizeDelta = new Vector2(800f, 60f);
        inventoryDim.SetActive(false);

        // Item bar along the bottom middle
        barRoot = new GameObject("Bar", typeof(RectTransform));
        barRoot.transform.SetParent(uiRoot.transform, false);
        var bar = (RectTransform)barRoot.transform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 40f);

        hintText = Label("Hint", uiRoot.transform, "", 22f, Vector2.zero);
        var hrt = hintText.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 165f);
        hrt.sizeDelta = new Vector2(600f, 30f);

        // Active effects, under the health bar
        effectsText = Label("Effects", uiRoot.transform, "", 24f, Vector2.zero);
        var ert = effectsText.rectTransform;
        ert.anchorMin = ert.anchorMax = new Vector2(0f, 1f);
        ert.pivot = new Vector2(0f, 1f);
        ert.anchoredPosition = new Vector2(30f, -110f);
        ert.sizeDelta = new Vector2(500f, 200f);
        effectsText.alignment = TextAlignmentOptions.TopLeft;

        // Messages
        toastText = Label("Toast", uiRoot.transform, "", 36f, Vector2.zero);
        var trt = toastText.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.72f);
        trt.sizeDelta = new Vector2(1400f, 160f); // room for two lines (new power messages)
        toastText.alpha = 0f;

        // Scroll reader: dim the screen, parchment panel in the middle
        reader = new GameObject("ScrollReader", typeof(RectTransform), typeof(Image));
        reader.transform.SetParent(uiRoot.transform, false);
        Fill((RectTransform)reader.transform, 0f);
        reader.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        reader.GetComponent<Image>().sprite = whiteSprite;
        RectTransform paper = Panel("Paper", reader.transform, new Color(0.9f, 0.82f, 0.62f));
        paper.anchorMin = paper.anchorMax = new Vector2(0.5f, 0.5f);
        paper.sizeDelta = new Vector2(1000f, 620f);
        readerTitle = Label("Title", paper, "", 44f, new Vector2(0f, 250f));
        readerTitle.color = new Color(0.25f, 0.15f, 0.08f);
        readerTitle.rectTransform.sizeDelta = new Vector2(900f, 70f);
        readerBody = Label("Text", paper, "", 30f, new Vector2(0f, -10f));
        readerBody.color = new Color(0.2f, 0.12f, 0.06f);
        readerBody.rectTransform.sizeDelta = new Vector2(880f, 420f);
        readerBody.alignment = TextAlignmentOptions.TopLeft;
        TMP_Text close = Label("Close", paper, "Press " + GameInput.KeyName(GameInput.GetKey(GameAction.Interact)) + " to close", 22f, new Vector2(0f, -280f));
        close.color = new Color(0.35f, 0.25f, 0.15f);
        reader.SetActive(false);
    }

    static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static void Fill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static TMP_Text Label(string name, Transform parent, string text, float size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(220f, 40f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}

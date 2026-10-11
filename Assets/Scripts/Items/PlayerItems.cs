using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// The player's items: a 4-slot quick bar at the bottom of the screen, the inventory screen, and the item effects.
//   Quick bar   - 1 / 2 / 3 / 4 picks a slot, Use Item (F / RT) uses the item in it. New items go into a free slot
//                 (not scrolls: they only go into the inventory).
//   Inventory   - Next Item (Tab / LT) opens it and pauses the game. It shows every item with what it does.
//                 Click an item to pick it, drag it onto one of the 4 slots to put it on the bar
//                 (or pick it and press 1-4). Use Item (F) or the Use button uses it. Tab, E or Esc closes.
//   A new game (Tutorial 1) starts with no items at all.
//   Shield            soaks up damage until it breaks or runs out of time
//   Poison Resistance poison hurts much less for a while (and cures current poison)
//   Healing           heals
//   Instant Revival   used automatically when you die: you get back up on the spot
//   Flying            for a while, hold Jump to rise and let go to float down
//   Scroll            opens as soon as it is picked up (the game pauses); it stays in the inventory to read again
//   Speed Boost       faster movement and faster attacks for a while
//   Invulnerability   nothing can hurt you for a few seconds
// All the numbers are in Assets/Resources/ItemSettings.asset.
// Nothing needs to be set up in scenes: this adds itself to the player in every scene.
[DefaultExecutionOrder(100)] // runs after the player's movement, so Flying can change the vertical speed
public class PlayerItems : MonoBehaviour
{
    static PlayerItems instance;
    const int Slots = 4;
    const string HotbarSave = "hotbar";
    const string FirstScene = "Tutorial_1_Move"; // a new game starts here: no items

    Health health;
    Rigidbody2D rb;
    PlayerController2D controller;
    PlayerCombat combat;
    int selectedSlot;        // quick bar slot (0-3)
    int selectedEntry;       // inventory screen: picked item

    // Static so they carry over to the next level. Cleared when you die or start a new game.
    // An "until you die" item sets its time to infinity.
    static float shieldTime, resistTime, speedTime;
    static int shieldLeft;
    float flyTime, invulnerableTime;

    GameObject uiRoot, barRoot, reader, inventoryScreen, gridRoot, useButton;
    TMP_Text effectsText, toastText, readerTitle, readerBody, infoName, infoText;
    RectTransform[] slotRects = new RectTransform[Slots];
    readonly List<RectTransform> gridBoxes = new List<RectTransform>();
    RectTransform dragGhost;
    int dragEntry = -1;
    Vector2 dragStart;
    bool dragging;
    float toastTimer;
    bool reading;
    int readerOpenedFrame; // the E that opened a note (a chest) must not close it right away
    bool inventoryOpen;

    ItemSettings S => ItemSettings.Current;

    // ---------------- Adds itself to the player in every scene ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        ClearBuffs(); // pressing Play starts with no leftover effects
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A new game: nothing but Raphael, his slash and his hook
        if (scene.name == FirstScene)
        {
            Inventory.Clear();
            ClearBuffs();
            RunOrder.NewGame(); // bosses, powers, Ariel and chests start fresh too
            PlayerPrefs.DeleteKey(HotbarSave);
            PlayerPrefs.Save();
        }
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerItems>()) pc.gameObject.AddComponent<PlayerItems>();
    }

    static void ClearBuffs()
    {
        shieldTime = resistTime = speedTime = 0f;
        shieldLeft = 0;
    }

    static float Lasts(bool untilDeath, float seconds) => untilDeath ? float.PositiveInfinity : seconds;
    static string TimeLeft(float t) => float.IsInfinity(t) ? "" : "  (" + Mathf.CeilToInt(t) + "s)";

    // Small message in the middle of the screen ("Got: Shield")
    public static void Toast(string message)
    {
        if (instance) instance.ShowToast(message);
    }

    // True while the inventory or a note is open (Esc closes that instead of opening the pause menu)
    public static bool ScreenOpen => instance && (instance.reading || instance.inventoryOpen);

    // Opens a note on screen and pauses the game (scrolls found in a level)
    public static void Read(string title, string text)
    {
        if (instance) instance.OpenReader(title, text);
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
            health.onDied.AddListener(ClearBuffs); // "until you die" items end here
        }
        LoadHotbar();
        BuildUi();
        Inventory.Changed += OnInventoryChanged;
        OnInventoryChanged();
    }

    void OnDestroy()
    {
        Inventory.Changed -= OnInventoryChanged;
        if (instance == this) instance = null;
        if (health)
        {
            health.DamageFilter = null;
            health.BeforeDeath = null;
        }
        if (reading || inventoryOpen) Time.timeScale = 1f;
        if (uiRoot) Destroy(uiRoot);
    }

    void OnInventoryChanged()
    {
        FillFreeSlots();
        RefreshBar();
        if (inventoryOpen) RefreshGrid();
    }

    // ---------------- Quick bar slots (saved) ----------------
    // Each slot remembers an item kind (and a scroll's title). A slot whose item is gone counts as free.

    [System.Serializable] class SlotData { public int type = -1; public string title; }
    [System.Serializable] class HotbarData { public SlotData[] slots; }
    SlotData[] slots = new SlotData[Slots];

    void LoadHotbar()
    {
        HotbarData data = null;
        string json = PlayerPrefs.GetString(HotbarSave, "");
        if (!string.IsNullOrEmpty(json)) data = JsonUtility.FromJson<HotbarData>(json);
        for (int i = 0; i < Slots; i++)
            slots[i] = data != null && data.slots != null && i < data.slots.Length && data.slots[i] != null ? data.slots[i] : new SlotData();
    }

    void SaveHotbar()
    {
        PlayerPrefs.SetString(HotbarSave, JsonUtility.ToJson(new HotbarData { slots = slots }));
        PlayerPrefs.Save();
    }

    // The inventory entry a slot points at (-1 = empty)
    int EntryOf(int slot)
    {
        SlotData d = slots[slot];
        if (d == null || d.type < 0) return -1;
        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        for (int i = 0; i < list.Count; i++)
            if ((int)list[i].type == d.type && (list[i].type != ItemType.Scroll || list[i].title == d.title)) return i;
        return -1;
    }

    bool OnBar(int entry)
    {
        for (int s = 0; s < Slots; s++) if (EntryOf(s) == entry) return true;
        return false;
    }

    // New items go into the first free slot, so they can be used right away
    void FillFreeSlots()
    {
        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (OnBar(i) || list[i].type == ItemType.Scroll) continue; // scrolls stay in the inventory only
            for (int s = 0; s < Slots; s++)
                if (EntryOf(s) < 0) { Assign(s, i, false); changed = true; break; }
        }
        if (changed) SaveHotbar();
    }

    // Puts inventory entry "entry" into quick bar slot "slot" (if it was in another slot, they swap)
    void Assign(int slot, int entry, bool save = true)
    {
        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        if (entry < 0 || entry >= list.Count) return;
        for (int s = 0; s < Slots; s++)
            if (s != slot && EntryOf(s) == entry) { slots[s] = slots[slot]; break; } // swap
        slots[slot] = new SlotData { type = (int)list[entry].type, title = list[entry].title };
        if (save) SaveHotbar();
        RefreshBar();
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        if (reading)
        {
            if (Time.frameCount != readerOpenedFrame && (GameInput.Pressed(GameAction.Interact) || GameInput.UseItemPressed || GameInput.PausePressed)) CloseReader();
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
        UpdateToast();

        if (Time.timeScale == 0f) return; // paused

        // 1-4 pick a quick bar slot
        Keyboard kb = Keyboard.current;
        if (kb != null)
            for (int i = 0; i < Slots; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { selectedSlot = i; RefreshBar(); ShowSlotName(i); }

        if (GameInput.NextItemPressed) { OpenInventory(); return; }
        if (GameInput.UseItemPressed)
        {
            int entry = EntryOf(selectedSlot);
            if (entry >= 0) Use(entry);
            else ShowToast("Slot " + (selectedSlot + 1) + " is empty. Press " + KeyName(GameAction.NextItem) + " to open your inventory.");
        }
    }

    void UpdateToast()
    {
        if (toastTimer <= 0f) return;
        toastTimer -= Time.unscaledDeltaTime;
        toastText.alpha = Mathf.Clamp01(toastTimer / 0.5f);
    }

    void ShowSlotName(int slot)
    {
        int entry = EntryOf(slot);
        if (entry < 0) { ShowToast("Slot " + (slot + 1) + ": empty", 1.2f); return; }
        Inventory.Entry e = Inventory.Entries[entry];
        ShowToast(e.count > 1 ? NameOf(e) + "  x" + e.count : NameOf(e), 1.5f);
    }

    // ---------------- Inventory screen (game paused) ----------------

    void OpenInventory()
    {
        inventoryOpen = true;
        Time.timeScale = 0f;
        selectedEntry = Mathf.Clamp(EntryOf(selectedSlot), 0, Mathf.Max(0, Inventory.Entries.Count - 1));
        inventoryScreen.SetActive(true);
        RefreshGrid();
    }

    void CloseInventory()
    {
        inventoryOpen = false;
        EndDrag();
        inventoryScreen.SetActive(false);
        Unpause();
        RefreshBar();
    }

    void InventoryControls()
    {
        UpdateToast();
        // Tab, E and Esc close it (Esc also opens the pause menu)
        if (GameInput.NextItemPressed || GameInput.PausePressed || GameInput.Pressed(GameAction.Interact)) { CloseInventory(); return; }

        int count = Inventory.Entries.Count;
        Keyboard kb = Keyboard.current;
        // Arrow keys pick an item; 1-4 put the picked item on that quick bar slot
        if (kb != null && count > 0)
        {
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { selectedEntry = (selectedEntry + 1) % count; RefreshGrid(); }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { selectedEntry = (selectedEntry + count - 1) % count; RefreshGrid(); }
            for (int i = 0; i < Slots; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Assign(i, selectedEntry); RefreshGrid(); }
        }
        if (GameInput.UseItemPressed && count > 0) UseFromScreen();

        MouseControls();
    }

    // Click to pick, drag onto a quick bar slot to put it there, click the Use button to use
    void MouseControls()
    {
        Mouse m = Mouse.current;
        if (m == null) return;
        Vector2 pos = m.position.ReadValue();

        if (m.leftButton.wasPressedThisFrame)
        {
            if (useButton.activeSelf && Over(useButton.transform as RectTransform, pos)) { UseFromScreen(); return; }
            for (int i = 0; i < gridBoxes.Count; i++)
                if (Over(gridBoxes[i], pos))
                {
                    selectedEntry = i;
                    dragEntry = i;
                    dragStart = pos;
                    RefreshGrid();
                    break;
                }
        }

        if (dragEntry >= 0 && m.leftButton.isPressed)
        {
            if (!dragging && (pos - dragStart).sqrMagnitude > 100f) StartDrag(dragEntry);
            if (dragging && dragGhost) dragGhost.position = pos;
        }

        if (dragEntry >= 0 && m.leftButton.wasReleasedThisFrame)
        {
            if (dragging)
                for (int s = 0; s < Slots; s++)
                    if (Over(slotRects[s], pos)) { Assign(s, dragEntry); RefreshGrid(); ShowToast(NameOf(Inventory.Entries[Mathf.Min(dragEntry, Inventory.Entries.Count - 1)]) + " is on slot " + (s + 1), 1.5f); break; }
            EndDrag();
        }
    }

    void StartDrag(int entry)
    {
        dragging = true;
        if (dragGhost) Destroy(dragGhost.gameObject);
        dragGhost = ItemBox("Dragging", uiRoot.transform, Inventory.Entries[entry], 90f, false);
        dragGhost.anchorMin = dragGhost.anchorMax = new Vector2(0f, 0f);
        foreach (Image img in dragGhost.GetComponentsInChildren<Image>()) img.color = new Color(img.color.r, img.color.g, img.color.b, img.color.a * 0.8f);
    }

    void EndDrag()
    {
        dragging = false;
        dragEntry = -1;
        if (dragGhost) Destroy(dragGhost.gameObject);
    }

    static bool Over(RectTransform rt, Vector2 screenPos) => rt && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

    void UseFromScreen()
    {
        int count = Inventory.Entries.Count;
        if (selectedEntry < 0 || selectedEntry >= count) return;
        int entry = selectedEntry;
        CloseInventory();
        Use(entry);
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
                shieldTime = Lasts(S.shieldUntilDeath, S.shieldDuration);
                break;
            case ItemType.PoisonResistance:
                resistTime = Lasts(S.poisonResistanceUntilDeath, S.poisonResistanceDuration);
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
                speedTime = Lasts(S.speedUntilDeath, S.speedDuration);
                break;
            case ItemType.Invulnerability:
                invulnerableTime = S.invulnerableDuration;
                if (health) health.StartIFrames(S.invulnerableDuration); // invincible and blinking
                break;
        }
        Inventory.RemoveOne(index);
        ShowToast("Used: " + itemName);
    }

    // What each item does, for the inventory screen
    string DescriptionOf(Inventory.Entry e)
    {
        switch (e.type)
        {
            case ItemType.Shield: return "Blocks the next " + S.shieldAmount + " damage (lasts " + (S.shieldUntilDeath ? "until it breaks or you die" : S.shieldDuration + " s") + ").";
            case ItemType.PoisonResistance: return "Poison hurts much less " + (S.poisonResistanceUntilDeath ? "until you die" : "for " + S.poisonResistanceDuration + " s") + (S.curesPoisonOnUse ? ", and cures poison now." : ".");
            case ItemType.Healing: return "Heals " + S.healAmount + " health.";
            case ItemType.InstantRevival: return "Used by itself when you die: you get back up with " + S.reviveHealth + " health.";
            case ItemType.Flying: return "For " + S.flyDuration + " s, hold " + KeyName(GameAction.Jump) + " to fly up, let go to float down.";
            case ItemType.Scroll: return "A note. Use it to read it again.";
            case ItemType.SpeedBoost: return "Move and attack faster " + (S.speedUntilDeath ? "until you die." : "for " + S.speedDuration + " s.");
            case ItemType.Invulnerability: return "Nothing can hurt you for " + S.invulnerableDuration + " s.";
        }
        return "";
    }

    string NameOf(Inventory.Entry e) => e.type == ItemType.Scroll && !string.IsNullOrEmpty(e.title) ? e.title : S.Look(e.type).name;

    static string KeyName(GameAction a) => GameInput.KeyName(GameInput.GetKey(a));

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
        readerOpenedFrame = Time.frameCount;
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
        if (shieldTime > 0f && shieldLeft > 0) s += "Shield " + shieldLeft + TimeLeft(shieldTime) + "\n";
        if (resistTime > 0f) s += "Poison resist" + TimeLeft(resistTime) + "\n";
        if (flyTime > 0f) s += "Flying  (" + Mathf.CeilToInt(flyTime) + "s)\n";
        if (speedTime > 0f) s += "Speed boost" + TimeLeft(speedTime) + "\n";
        if (invulnerableTime > 0f) s += "Invulnerable  (" + Mathf.CeilToInt(invulnerableTime) + "s)\n";
        int revives = Inventory.Count(ItemType.InstantRevival);
        if (revives > 0) s += "Revival ready x" + revives;
        effectsText.text = s;
    }

    // The 4 quick bar slots along the bottom middle
    void RefreshBar()
    {
        if (!barRoot) return;
        for (int i = barRoot.transform.childCount - 1; i >= 0; i--) Destroy(barRoot.transform.GetChild(i).gameObject);

        const float size = 90f, gap = 12f;
        float startX = -(Slots - 1) * (size + gap) * 0.5f;
        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        for (int s = 0; s < Slots; s++)
        {
            int entry = EntryOf(s);
            RectTransform slot = entry >= 0
                ? ItemBox("Slot", barRoot.transform, list[entry], size, s == selectedSlot)
                : EmptyBox("Slot", barRoot.transform, size, s == selectedSlot);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            slot.anchoredPosition = new Vector2(startX + s * (size + gap), size * 0.5f);
            Label("Number", slot, (s + 1).ToString(), 20f, new Vector2(-size * 0.5f + 12f, size * 0.5f - 14f));
            slotRects[s] = slot;
        }
    }

    // The inventory screen's grid of every item, and the info about the picked one
    void RefreshGrid()
    {
        if (!gridRoot) return;
        for (int i = gridRoot.transform.childCount - 1; i >= 0; i--) Destroy(gridRoot.transform.GetChild(i).gameObject);
        gridBoxes.Clear();

        IReadOnlyList<Inventory.Entry> list = Inventory.Entries;
        selectedEntry = Mathf.Clamp(selectedEntry, 0, Mathf.Max(0, list.Count - 1));
        const int columns = 8;
        const float size = 96f, gap = 14f;
        for (int i = 0; i < list.Count; i++)
        {
            RectTransform box = ItemBox("Item", gridRoot.transform, list[i], size, i == selectedEntry);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            int row = i / columns, col = i % columns;
            int inRow = Mathf.Min(columns, list.Count - row * columns);
            box.anchoredPosition = new Vector2((col - (inRow - 1) * 0.5f) * (size + gap), -size * 0.5f - row * (size + gap));
            for (int s = 0; s < Slots; s++)
                if (EntryOf(s) == i) Label("OnBar", box, (s + 1).ToString(), 18f, new Vector2(-size * 0.5f + 12f, size * 0.5f - 13f)).color = new Color(1f, 0.85f, 0.3f);
            gridBoxes.Add(box);
        }

        bool any = list.Count > 0;
        useButton.SetActive(any);
        if (any)
        {
            Inventory.Entry e = list[selectedEntry];
            infoName.text = NameOf(e) + (e.count > 1 ? "  x" + e.count : "");
            infoText.text = DescriptionOf(e) + "\n<size=80%><color=#B8B0C8>Drag it onto a slot below (or press 1-4) to put it on your bar.</color></size>";
        }
        else
        {
            infoName.text = "No items yet";
            infoText.text = "Open chests and pick things up on your way.";
        }
    }

    // A box with an item's picture (and how many)
    RectTransform ItemBox(string name, Transform parent, Inventory.Entry e, float size, bool highlighted)
    {
        ItemLook look = S.Look(e.type);
        RectTransform box = Panel(name, parent, highlighted ? new Color(1f, 0.85f, 0.3f) : new Color(0.1f, 0.08f, 0.14f, 0.9f));
        box.sizeDelta = new Vector2(size, size);
        RectTransform inner = Panel("Inner", box, new Color(0.1f, 0.08f, 0.14f, 0.95f));
        Fill(inner, 4f);
        RectTransform pic = Panel("Picture", inner, look.color);
        Fill(pic, size * 0.15f);
        if (look.icon)
        {
            var img = pic.GetComponent<Image>();
            img.sprite = look.icon;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        if (e.count > 1) Label("Count", box, "x" + e.count, 22f, new Vector2(size * 0.5f - 18f, -size * 0.5f + 14f));
        return box;
    }

    RectTransform EmptyBox(string name, Transform parent, float size, bool highlighted)
    {
        RectTransform box = Panel(name, parent, highlighted ? new Color(1f, 0.85f, 0.3f, 0.8f) : new Color(0.1f, 0.08f, 0.14f, 0.6f));
        box.sizeDelta = new Vector2(size, size);
        RectTransform inner = Panel("Inner", box, new Color(0.05f, 0.04f, 0.08f, 0.7f));
        Fill(inner, 4f);
        return box;
    }

    void BuildUi()
    {

        uiRoot = new GameObject("InventoryUI");
        var canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // The inventory screen (made first, so the quick bar draws on top of its dim and stays a drop target)
        inventoryScreen = new GameObject("InventoryScreen", typeof(RectTransform), typeof(Image));
        inventoryScreen.transform.SetParent(uiRoot.transform, false);
        Fill((RectTransform)inventoryScreen.transform, 0f);
        var dim = inventoryScreen.GetComponent<Image>();
        dim.sprite = WhiteSprite;
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = false;

        RectTransform window = Panel("Window", inventoryScreen.transform, new Color(0.08f, 0.06f, 0.12f, 0.95f));
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.anchoredPosition = new Vector2(0f, 90f);
        window.sizeDelta = new Vector2(1000f, 560f);
        TMP_Text title = Label("Title", window, "INVENTORY  (paused)", 38f, new Vector2(0f, 240f));
        title.rectTransform.sizeDelta = new Vector2(900f, 60f);

        gridRoot = new GameObject("Grid", typeof(RectTransform));
        gridRoot.transform.SetParent(window, false);
        var grt = (RectTransform)gridRoot.transform;
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
        grt.anchoredPosition = new Vector2(0f, -90f);

        infoName = Label("ItemName", window, "", 30f, new Vector2(-90f, -150f));
        infoName.rectTransform.sizeDelta = new Vector2(760f, 44f);
        infoName.alignment = TextAlignmentOptions.Left;
        infoName.color = new Color(1f, 0.85f, 0.3f);
        infoText = Label("ItemInfo", window, "", 24f, new Vector2(-90f, -210f));
        infoText.rectTransform.sizeDelta = new Vector2(760f, 80f);
        infoText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform use = Panel("UseButton", window, new Color(1f, 0.85f, 0.3f));
        use.anchorMin = use.anchorMax = new Vector2(0.5f, 0.5f);
        use.anchoredPosition = new Vector2(400f, -175f);
        use.sizeDelta = new Vector2(150f, 60f);
        TMP_Text useText = Label("Text", use, "USE", 28f, Vector2.zero);
        useText.color = new Color(0.1f, 0.06f, 0.1f);
        useButton = use.gameObject;

        TMP_Text keys = Label("Keys", window, "", 20f, new Vector2(0f, -258f));
        keys.rectTransform.sizeDelta = new Vector2(940f, 30f);
        keys.color = new Color(0.72f, 0.69f, 0.78f);
        keys.text = "Click to pick   -   Drag onto a slot   -   [" + KeyName(GameAction.UseItem) + "] or USE to use   -   [" +
                    KeyName(GameAction.NextItem) + "] / [" + KeyName(GameAction.Interact) + "] close";
        inventoryScreen.SetActive(false);

        // The 4-slot quick bar along the bottom middle
        barRoot = new GameObject("Bar", typeof(RectTransform));
        barRoot.transform.SetParent(uiRoot.transform, false);
        var bar = (RectTransform)barRoot.transform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 30f);

        // Active effects, under the health bar
        effectsText = Label("Effects", uiRoot.transform, "", 24f, Vector2.zero);
        var ert = effectsText.rectTransform;
        ert.anchorMin = ert.anchorMax = new Vector2(0f, 1f);
        ert.pivot = new Vector2(0f, 1f);
        ert.anchoredPosition = new Vector2(30f, -145f); // under the health bar frame
        ert.sizeDelta = new Vector2(500f, 200f);
        effectsText.alignment = TextAlignmentOptions.TopLeft;

        // Messages
        toastText = Label("Toast", uiRoot.transform, "", 36f, Vector2.zero);
        var trt = toastText.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.595f); // under the boss bars, above the "continue?" box
        trt.sizeDelta = new Vector2(1400f, 160f); // room for two lines (new power messages)
        toastText.alpha = 0f;

        // Scroll reader: dim the screen, parchment panel in the middle
        reader = new GameObject("ScrollReader", typeof(RectTransform), typeof(Image));
        reader.transform.SetParent(uiRoot.transform, false);
        Fill((RectTransform)reader.transform, 0f);
        reader.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        reader.GetComponent<Image>().sprite = WhiteSprite;
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
        TMP_Text close = Label("Close", paper, "Press " + KeyName(GameAction.Interact) + " to close", 22f, new Vector2(0f, -280f));
        close.color = new Color(0.35f, 0.25f, 0.15f);
        reader.SetActive(false);
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

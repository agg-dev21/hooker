using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
public partial class PlayerItems : MonoBehaviour
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
}
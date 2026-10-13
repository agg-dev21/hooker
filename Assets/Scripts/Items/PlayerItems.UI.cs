using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// Part of PlayerItems (see PlayerItems.cs for its settings and the main loop).
// Building and refreshing the item UI: quick bar, inventory grid, scroll reader, messages.
public partial class PlayerItems
{
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

    // What the effects text is currently showing: shield amount, then the whole seconds left on each effect
    // (0 = not active, -1 = lasts until death), then the number of revivals. Only when one of these changes
    // is the text rebuilt, so normally this costs nothing (it used to build a new string every frame).
    readonly int[] effectsShown = new int[7];
    readonly int[] effectsNow = new int[7];

    static int SecondsShown(float t) => t <= 0f ? 0 : float.IsInfinity(t) ? -1 : Mathf.CeilToInt(t);

    void ShowEffects()
    {
        bool shield = shieldTime > 0f && shieldLeft > 0;
        effectsNow[0] = shield ? shieldLeft : 0;
        effectsNow[1] = shield ? SecondsShown(shieldTime) : 0;
        effectsNow[2] = SecondsShown(resistTime);
        effectsNow[3] = SecondsShown(flyTime);
        effectsNow[4] = SecondsShown(speedTime);
        effectsNow[5] = SecondsShown(invulnerableTime);
        effectsNow[6] = Inventory.Count(ItemType.InstantRevival);

        bool changed = false;
        for (int i = 0; i < effectsNow.Length; i++)
            if (effectsNow[i] != effectsShown[i]) { effectsShown[i] = effectsNow[i]; changed = true; }
        if (!changed) return; // same text as last frame

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
        bar.pivot = new Vector2(0.5f, 0f);          // measured from its bottom edge...
        bar.sizeDelta = new Vector2(440f, 90f);     // ...as big as the 4 slots (they sit on its bottom edge)
        bar.anchoredPosition = new Vector2(0f, 28f); // so the whole bar is on screen, 28 above the bottom

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
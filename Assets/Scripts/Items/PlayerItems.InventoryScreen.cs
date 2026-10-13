using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Part of PlayerItems (see PlayerItems.cs for its settings and the main loop).
// Opening, closing and using the inventory screen (keyboard, controller and mouse drag).
public partial class PlayerItems
{
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
}

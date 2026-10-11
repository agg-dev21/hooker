using UnityEngine;
using UnityEngine.Events;

// A chest. Stand next to it and press Interact (E / controller Y by default) to open it.
// It can give an item (pick it below); a scroll gets the Title and Text you type here.
// The On Opened event is also there for anything extra.
// Needs a SpriteRenderer and a Collider2D with Is Trigger ON (the area where the player can open it).
public class Chest : MonoBehaviour
{
    [SerializeField] Color openedColor = new Color(0.35f, 0.25f, 0.15f);
    [SerializeField] Vector3 openedScale = new Vector3(1.5f, 0.6f, 1f); // placeholder "lid open" look
    [SerializeField] GameObject prompt; // "Press E" pop-up, shown while the player is next to the closed chest
    public UnityEvent onOpened;

    [Header("Item inside")]
    [SerializeField] bool givesItem = true;
    [SerializeField] ItemType item = ItemType.Healing;
    [SerializeField] int amount = 1;
    [SerializeField] string scrollTitle = "A torn note";
    [SerializeField, TextArea(3, 10)] string scrollText = "Write the hint about the mother here.";

    [Header("Bonus item (optional second item)")]
    [SerializeField] bool givesBonus = false;
    [SerializeField] ItemType bonusItem = ItemType.Healing;
    [SerializeField] int bonusAmount = 1;

    public bool IsOpen { get; private set; }

    bool playerNear;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (prompt) prompt.SetActive(false);
    }

    void Update()
    {
        if (IsOpen || !playerNear || Time.timeScale == 0f) return;
        if (GameInput.InteractPressed) Open(); // Interact (E / controller Y by default, changeable in Controls)
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (sr) sr.color = openedColor;
        transform.localScale = openedScale;
        if (prompt) prompt.SetActive(false);
        if (givesItem)
        {
            string itemName = ItemSettings.Current.Look(item).name;
            PlayerItems.Toast(Inventory.Add(item, amount, scrollTitle, scrollText)
                ? "Got: " + itemName + (amount > 1 ? " x" + amount : "")
                : itemName + " is full");
        }
        if (givesBonus)
        {
            string bonusName = ItemSettings.Current.Look(bonusItem).name;
            if (Inventory.Add(bonusItem, bonusAmount, scrollTitle, scrollText))
                PlayerItems.Toast("Got: " + ItemSettings.Current.Look(item).name + " + " + bonusName);
        }
        onOpened?.Invoke();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = true;
        if (prompt && !IsOpen) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = false;
        if (prompt) prompt.SetActive(false);
    }
}
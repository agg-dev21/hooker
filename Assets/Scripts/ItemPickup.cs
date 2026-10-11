using UnityEngine;

// An item lying in a level. Touch it to put it in your inventory. Bobs up and down.
// For a scroll, type its Title and Text below.
// Needs a Collider2D with Is Trigger ON (Tools > Items > Add Item Pickup sets everything up).
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    [SerializeField] ItemType item = ItemType.Healing;
    [SerializeField] int amount = 1;
    [SerializeField] string scrollTitle = "A torn note";
    [SerializeField, TextArea(3, 10)] string scrollText = "Write the hint about the mother here.";
    [SerializeField] float bobHeight = 0.15f;

    Vector3 start;
    SpriteRenderer sr;

    void Start()
    {
        start = transform.position;
        sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = ItemSettings.Current.Look(item).color;
    }

    void Update()
    {
        transform.position = start + Vector3.up * Mathf.Sin(Time.time * 3f) * bobHeight;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        if (!Inventory.Add(item, amount, scrollTitle, scrollText))
        {
            PlayerItems.Toast(ItemSettings.Current.Look(item).name + " is full");
            return;
        }
        if (item == ItemType.Scroll) PlayerItems.Read(scrollTitle, scrollText); // read it now (game paused), kept in the inventory
        else PlayerItems.Toast("Got: " + ItemSettings.Current.Look(item).name + (amount > 1 ? " x" + amount : ""));
        Destroy(gameObject);
    }
}

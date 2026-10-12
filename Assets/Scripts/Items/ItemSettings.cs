using UnityEngine;

// Every kind of item the player can carry
public enum ItemType
{
    Shield, PoisonResistance, Healing, InstantRevival, Flying, Scroll, SpeedBoost, Invulnerability
}

// How each item looks in the inventory bar (name, colour, optional picture)
[System.Serializable]
public class ItemLook
{
    public string name;
    public Color color = Color.white;
    public Sprite icon; // optional: drop in your team's art later
}

// All item settings in one place: Assets/Resources/ItemSettings.asset
// Click it in the Project window to change how strong / long every item is.
[CreateAssetMenu(fileName = "ItemSettings", menuName = "Hooker/Item Settings")]
public class ItemSettings : ScriptableObject
{
    [Header("Shield (absorbs damage)")]
    public int shieldAmount = 40;          // damage it soaks up before breaking
    public float shieldDuration = 15f;     // seconds before it fades even if not used up
    public bool shieldUntilDeath = true;   // ON: lasts until it breaks or you die (the seconds above are ignored)

    [Header("Poison Resistance")]
    [Range(0f, 1f)] public float poisonResistance = 0.75f; // part of poison damage blocked (1 = immune)
    public float poisonResistanceDuration = 20f;
    public bool poisonResistanceUntilDeath = true; // ON: lasts until you die (the seconds above are ignored)
    public bool curesPoisonOnUse = true;

    [Header("Healing")]
    public int healAmount = 40;

    [Header("Instant Revival (used automatically when you die)")]
    public int reviveHealth = 50;
    public float reviveInvulnerable = 2f;

    [Header("Flying (hold Jump to rise, let go to float down)")]
    public float flyDuration = 6f;
    public float flyUpSpeed = 9f;
    public float flyFallSpeed = 2f;        // slowest fall while flying

    [Header("Speed Boost (movement and attacks)")]
    public float moveSpeedMultiplier = 1.4f;
    public float attackSpeedMultiplier = 1.5f; // attacks recharge this many times faster
    public float speedDuration = 10f;
    public bool speedUntilDeath = true;    // ON: lasts until you die (the seconds above are ignored)

    [Header("Invulnerability")]
    public float invulnerableDuration = 4f;

    [Header("Inventory")]
    public int maxStack = 9;               // most of one item you can carry (scrolls are kept one by one)

    [Header("Chests (filled by Tools > Art > Ariel And Items)")]
    public Sprite chestClosed;             // empty: the plain gold box
    public Sprite chestOpen;
    public float chestScale = 1f;          // size of the chest drawing (1 = about 2 tiles wide)

    [Header("Looks (in the same order as the items)")]
    public ItemLook[] looks =
    {
        new ItemLook { name = "Shield",           color = new Color(0.35f, 0.65f, 1f) },
        new ItemLook { name = "Poison Resist",    color = new Color(0.45f, 0.9f, 0.3f) },
        new ItemLook { name = "Healing",          color = new Color(0.95f, 0.25f, 0.3f) },
        new ItemLook { name = "Instant Revival",  color = new Color(1f, 0.85f, 0.3f) },
        new ItemLook { name = "Flying",           color = new Color(0.75f, 0.9f, 1f) },
        new ItemLook { name = "Scroll",           color = new Color(0.9f, 0.8f, 0.6f) },
        new ItemLook { name = "Speed Boost",      color = new Color(1f, 0.6f, 0.15f) },
        new ItemLook { name = "Invulnerable",     color = new Color(0.85f, 0.5f, 1f) },
    };

    public ItemLook Look(ItemType type)
    {
        int i = (int)type;
        return looks != null && i < looks.Length ? looks[i] : new ItemLook { name = type.ToString() };
    }

    static ItemSettings current;

    // The settings asset (or the defaults above if it is missing)
    public static ItemSettings Current
    {
        get
        {
            if (!current) current = Resources.Load<ItemSettings>("ItemSettings");
            if (!current) current = CreateInstance<ItemSettings>();
            return current;
        }
    }
}

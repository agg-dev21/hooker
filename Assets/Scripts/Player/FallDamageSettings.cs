using UnityEngine;

// Fall damage numbers for the whole game (Assets/Resources/FallDamageSettings.asset).
// Select the asset to change them; every scene uses the same values. Used by PlayerFallDamage.
[CreateAssetMenu(fileName = "FallDamageSettings", menuName = "Hooker/Fall Damage Settings")]
public class FallDamageSettings : ScriptableObject
{
    public bool fallDamageOn = true;
    public float safeHeight = 12f;      // falls up to this many tiles do nothing (a full held jump is about 8.5)
    public int damagePerTile = 2;       // damage for every tile fallen past the safe height
    public int minDamage = 3;           // smallest hit once a fall counts
    public int maxDamage = 25;          // biggest hit from one fall (the player has 100 health)
    public bool dashDodgesFallDamage = false; // landing while dash-invincible skips the damage

    static FallDamageSettings current;
    static bool loaded;

    // The asset in Resources, or plain default values if it is missing
    public static FallDamageSettings Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<FallDamageSettings>("FallDamageSettings");
                if (!current) current = CreateInstance<FallDamageSettings>();
                loaded = true;
            }
            return current;
        }
    }
}
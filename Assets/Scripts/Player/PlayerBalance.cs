using UnityEngine;

// Player buffs for the whole game, in one place (Assets/Resources/PlayerBalance.asset).
// The player is saved separately in every scene, so these apply on top of each scene's own numbers.
// Select the asset to change them. Max health is not changed here.
[CreateAssetMenu(fileName = "PlayerBalance", menuName = "Hooker/Player Balance")]
public class PlayerBalance : ScriptableObject
{
    public float punchDamageMultiplier = 1.5f;  // slash damage x this (4 -> 6)
    public float hookDamageMultiplier = 2f;     // hook damage x this (2 / 3 -> 4 / 6)
    public int poisonHookTickBonus = 1;         // poison hook: this much more per tick (2 -> 3)
    public float hitInvincibleTime = 1.4f;      // after you are hit you can't be hit again for this long (was 1)
    [Range(0.6f, 1.2f)] public float cameraZoom = 0.9f; // camera view size x this (1 = as set in each scene, smaller = closer)

    static PlayerBalance current;
    static bool loaded;

    public static PlayerBalance Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<PlayerBalance>("PlayerBalance");
                if (!current) current = CreateInstance<PlayerBalance>();
                loaded = true;
            }
            return current;
        }
    }
}

using UnityEngine;

// Every number for the companion, in one place for the whole game
// (Assets/Resources/CompanionSettings.asset: select it to change them). Used by Companion.
[CreateAssetMenu(fileName = "CompanionSettings", menuName = "Hooker/Companion Settings")]
public class CompanionSettings : ScriptableObject
{
    [Header("Following")]
    public float followDistance = 1.6f;   // stays this far behind the player
    public float followHeight = 0.4f;     // and this much higher (she floats)
    public float followSmoothTime = 0.25f;
    public float bobHeight = 0.15f;       // gentle up and down float
    public float bobSpeed = 2f;

    [Header("Hiding (she fades away near danger)")]
    public float dangerRadius = 12f;      // enemies, obstacles or a boss this close: she hides
    public float safeTimeToReturn = 1.2f; // danger gone this long: she comes back
    public float fadeTime = 0.4f;
    public float joinStayTime = 5f;       // just freed (or a new scene): she stays out this long no matter what
    public float minShowTime = 2f;        // once she comes back she stays at least this long (no flickering)
    public bool ignoreDangerBehindWalls = true; // enemies on the other side of a wall or floor don't scare her

    [Header("Light beam (summon key: R / RS click)")]
    // A column of light strikes down from the sky onto the nearest enemy in front of you
    // (or Strike Distance ahead if there is none). Everything inside the column is hit once.
    public float cooldown = 10f;
    public float chargeTime = 0.35f;      // she glows before the strike
    public float beamLength = 18f;        // how far ahead she looks for an enemy
    public float strikeDistance = 6f;     // where it lands when there is no enemy
    public float beamThickness = 3f;      // width of the column of light
    public float beamHeight = 10f;        // height of the column
    public int beamDamage = 25;           // to every enemy and boss in the column
    public float beamShowTime = 0.35f;    // (no art) how long the plain column shows
    public float beamFps = 14f;           // (art) speed of the strike animation
    public int beamHitFrame = 4;          // (art) the frame where the light hits the ground and deals damage

    [Header("Aiming (press R, then click where the light falls)")]
    public float aimTime = 1.5f;          // no click in this time: she fires 3 short beams around you by herself
    public float tapTime = 0.25f;         // a click shorter than this = 3 short beams; longer = charging one strong beam
    public float maxChargeTime = 1.5f;    // holding this long gives the strongest beam
    public float cursorSpeed = 14f;       // controller: how fast the right stick moves the target
    public float maxAimDistance = 16f;    // the target can't go further than this from you
    public float scatterRadius = 2.5f;    // the 3 short beams land this close to where you clicked
    public float autoRadius = 6f;         // no click: the 3 beams land in a circle this big around you

    [Header("Short beams (3 of them)")]
    public float shortBeamWidth = 1.5f;
    public float shortBeamTime = 0.3f;    // how long each stays
    public int shortBeamDamage = 10;

    [Header("Strong beam (held), from a quick hold to a full charge")]
    public float strongWidthMin = 2.5f;
    public float strongWidthMax = 5f;
    public float strongTimeMin = 0.5f;
    public float strongTimeMax = 1.2f;
    public int strongDamageMin = 25;
    public int strongDamageMax = 60;

    [Header("Art (filled by Tools > Art > Ariel And Items)")]
    public Sprite[] idleFrames;           // floating
    public float idleFps = 3f;
    public Sprite[] appearFrames;         // popping in
    public float appearFps = 14f;
    public Sprite[] beamFrames;           // the column of light (pivot at the bottom)

    [Header("Look (placeholder until her art is in)")]
    public Vector2 size = new Vector2(0.8f, 1.6f);
    public Color color = new Color(1f, 0.92f, 0.65f);
    public Color beamColor = new Color(1f, 0.97f, 0.75f, 0.9f);
    public Color glowColor = new Color(1f, 0.9f, 0.6f);
    public Sprite sprite;                 // her drawing (empty = a plain box)

    static CompanionSettings current;
    static bool loaded;

    public static CompanionSettings Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<CompanionSettings>("CompanionSettings");
                if (!current) current = CreateInstance<CompanionSettings>();
                loaded = true;
            }
            return current;
        }
    }
}

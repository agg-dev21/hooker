using UnityEngine;

// The health bar art (Assets/Resources/HealthBarSkin.asset, made by Tools > Art > Health Bars).
// One look for Raphael's bar, one for each boss (Hardy, Oscar, Oscar + Madame O in Level 3, KK), and a plain boss
// look for anything else. Each frame has a see-through window exactly where the fill goes; the fill is cut by the
// game to match the health, so no animation is needed. A look with no fill picture uses a plain fill in Fill Color.
[CreateAssetMenu(fileName = "HealthBarSkin", menuName = "Hooker/Health Bar Skin")]
public class HealthBarSkin : ScriptableObject
{
    [System.Serializable]
    public class Look
    {
        public Sprite frame;           // drawn on top of the fill
        public Sprite fill;            // the part that shrinks (empty: a plain bar in Fill Color)
        public Color fillColor = Color.white; // tint of the fill
        public bool fillOnTop;         // ON: the frame is the empty bar drawn behind, the fill picture goes on top of it
        public Vector2 framePixels;    // size of the frame picture
        public Rect windowPixels;      // the window in the frame (from its top-left corner), same size as the fill
    }

    public Look player;
    public Look boss;          // any boss without its own look below
    public Look hardy;
    public Look oscar;         // Level 2 (alone)
    public Look oscarLevel3;   // Level 3, next to Madame O
    public Look madameO;
    public Look kk;

    // The look for this boss's bar (falls back to the plain boss look)
    public Look ForBoss(Health target)
    {
        Look look = null;
        if (target)
        {
            if (target.GetComponent<HardyBoss>()) look = hardy;
            else if (target.GetComponent<FatherBoss>()) look = kk;
            else if (target.GetComponent<OctopusBoss>()) look = madameO;
            else if (target.GetComponent<PoisonBoss>())
                look = FindAnyObjectByType<OctopusBoss>(FindObjectsInactive.Include) ? oscarLevel3 : oscar;
        }
        return look != null && look.frame ? look : boss;
    }

    static HealthBarSkin current;
    static bool loaded;

    public static HealthBarSkin Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<HealthBarSkin>("HealthBarSkin");
                loaded = true;
            }
            return current;
        }
    }
}

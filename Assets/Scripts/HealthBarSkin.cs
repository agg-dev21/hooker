using UnityEngine;

// The health bar art (Assets/Resources/HealthBarSkin.asset, made by Tools > Art > Health Bars).
// One look for Raphael's bar and one for every boss bar. Each frame has a see-through window exactly where the
// fill goes; the fill picture is cut by the game to match the health, so no animation is needed.
[CreateAssetMenu(fileName = "HealthBarSkin", menuName = "Hooker/Health Bar Skin")]
public class HealthBarSkin : ScriptableObject
{
    [System.Serializable]
    public class Look
    {
        public Sprite frame;           // drawn on top of the fill
        public Sprite fill;            // the part that shrinks
        public Vector2 framePixels;    // size of the frame picture
        public Rect windowPixels;      // the window in the frame (from its top-left corner), same size as the fill
    }

    public Look player;
    public Look boss;

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

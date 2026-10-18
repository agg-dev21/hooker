using UnityEngine;
using UnityEngine.UI;

// The pictures for the player's special abilities, kept in one asset so every scene can use them
// without setting anything up. Lives at Assets/Resources/AbilityArt.asset
// (Tools > Art > Hardy creates it and fills the spikes; Tools > Art > Ability Icons And Ground Effects fills the icons).
[CreateAssetMenu(fileName = "AbilityArt", menuName = "Hooker/Ability Art")]
public class AbilityArt : ScriptableObject
{
    public Sprite[] hardySpikesFrames; // the attack animation (drawn growing to the left)
    public Sprite hardySpikesIcon;     // the picture in the ability icon

    [Header("Ability icons (each picture has its own frame drawn in)")]
    public Sprite chainIcon;           // Madame O's chain (PlayerChain)
    public Sprite arielIcon;           // Ariel's light beam (Companion)
    public Sprite poisonIcon;          // Oscar's poison hook (always on, so it has no key)

    static AbilityArt current;
    static bool loaded;

    public static AbilityArt Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<AbilityArt>("AbilityArt");
                loaded = true;
            }
            return current;
        }
    }

    // Puts an icon picture over a whole ability square. The picture has its own frame, so the plain square and its
    // border are hidden. Call it before the cooldown sweep is added, so the sweep stays on top.
    // False when there is no picture (keep the placeholder).
    public static bool Dress(RectTransform frame, RectTransform border, Sprite icon)
    {
        if (!icon || !frame) return false;
        if (frame.TryGetComponent(out Image bg)) bg.color = Color.clear;
        if (border && border.TryGetComponent(out Image b)) b.color = Color.clear;
        var go = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(frame, false);
        UiKit.Fill((RectTransform)go.transform, 0f);
        var img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return true;
    }
}

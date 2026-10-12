using UnityEngine;
using UnityEngine.SceneManagement;

// The drawings of the hook rings (Assets/Resources/HookPointArt.asset, made by Tools > Art > Hook Points).
// One drawing per part of the game, so each level's rings match its boss:
//   Tutorial and Level 1 (Hardy) / Level 2 (Oscar) / Level 3 (Madame O) / Level 4 (KK).
// Every hook ring (HookPoint) picks it up by itself when the game runs. Empty: the plain ring box.
[CreateAssetMenu(fileName = "HookPointArt", menuName = "Hooker/Hook Point Art")]
public class HookPointArt : ScriptableObject
{
    public Sprite tutorialAndLevel1;
    public Sprite level2;
    public Sprite level3;
    public Sprite level4;
    public float drawScale = 1f; // size of the drawings (1 = about 1.6 tiles tall for the first one)

    static HookPointArt current;
    static bool loaded;

    public static HookPointArt Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<HookPointArt>("HookPointArt");
                loaded = true;
            }
            return current;
        }
    }

    // The drawing for the scene that is playing
    public Sprite ForScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        Sprite s = scene.StartsWith("Level_2") ? level2
                 : scene.StartsWith("Level_3") ? level3
                 : scene.StartsWith("Level_4") ? level4
                 : tutorialAndLevel1;
        return s ? s : tutorialAndLevel1;
    }
}

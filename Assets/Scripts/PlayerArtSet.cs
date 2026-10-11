using UnityEngine;

// The main character's drawings and animation speeds (Assets/Resources/PlayerArtSet.asset).
// Made and filled by Tools > Set Up Player Art. Select it to change the speeds. Used by PlayerArt.
// Frame 0 of every sheet is the standing pose; the animations use the frames after it.
[CreateAssetMenu(fileName = "PlayerArtSet", menuName = "Hooker/Player Art Set")]
public class PlayerArtSet : ScriptableObject
{
    [Header("Frames (filled by Tools > Set Up Player Art)")]
    public Sprite[] run;      // 0 standing, 1-4 running
    public Sprite[] jump;     // 0 standing, 1 going up, 2 falling, 3 landing
    public Sprite[] dash;     // 0 standing, 1 start, 2-3 speed blur, 4 stop, 5 standing
    public Sprite[] attack;   // 0 standing, 1-4 slash
    public Sprite[] hook;     // 0 standing, 1-3 throwing the hook
    public bool drawnFacingRight = true;

    [Header("Speeds (frames per second)")]
    public float runFps = 12f;
    public float attackFps = 20f;
    public float hookFps = 14f;
    public float dashFps = 18f;
    public float landTime = 0.1f;   // how long the landing frame shows

    [Header("Placement")]
    public float heightOffset = 0f; // moves the drawing up (+) or down (-) from his feet

    static PlayerArtSet current;
    static bool loaded;

    public static PlayerArtSet Current
    {
        get
        {
            if (!loaded)
            {
                current = Resources.Load<PlayerArtSet>("PlayerArtSet");
                loaded = true;
            }
            return current;
        }
    }
}

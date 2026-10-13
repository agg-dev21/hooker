using UnityEngine;

// The plank drawings that dress the levels (Assets/Resources/PlankArt.asset, made by Tools > Art > Planks).
// Each part of the game has its own set, to match the place it is (used by PlankDressing):
//   Tutorial and Level 1 (Hardy's ship, the dock): wood
//   Level 2 (Oscar's overgrown poison tower):        grassy wood
//   Level 3 (the sunken prison):                     metal
//   Level 4 (KK's flooded hall):                     wood
[CreateAssetMenu(fileName = "PlankArt", menuName = "Hooker/Plank Art")]
public class PlankArt : ScriptableObject
{
    [System.Serializable]
    public class Set
    {
        public Sprite floor;     // laid along the top of every floor and ledge (repeats)
        public Sprite platform;  // the thin jump-through platforms (ends stay, middle repeats)
        public Sprite post;      // optional: a support post under floating platforms, down to the floor
    }

    public Set wood;
    public Set grassy;
    public Set metal;

    [Header("Which set each part of the game uses")]
    public bool level4UsesMetal = false; // OFF: KK's flooded hall uses wood

    [Header("Look")]
    public bool dressingOn = true;       // OFF: the levels look like before (plain tiles)
    public float maxPostLength = 8f;     // a post is only added when the floor is at most this far below a platform

    static PlankArt current;
    static bool loaded;

    public static PlankArt Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<PlankArt>("PlankArt");
                loaded = true;
            }
            return current;
        }
    }

    // The set for this scene, or null for scenes that don't get planks (menus, the boat ride, the ending)
    public Set ForScene(string scene)
    {
        if (scene.StartsWith("Level_2")) return grassy;
        if (scene.StartsWith("Level_3")) return metal;
        if (scene.StartsWith("Level_4")) return level4UsesMetal ? metal : wood;
        if (scene == "Level_1_Sail") return null; // on the open sea
        if (scene.StartsWith("Level_1") || scene.StartsWith("Tutorial")) return wood;
        return null;
    }
}

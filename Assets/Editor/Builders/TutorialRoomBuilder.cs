using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Editor-only. Settings and helpers shared by the Level 1 builders in the other parts of this class
// (TutorialRoomBuilder.Level1, .Level1Deck, .Level1Sail). The tutorial rooms are built by TutorialRebuilder.
public static partial class TutorialRoomBuilder
{
    const string ScenesFolder = "Assets/Scenes";
    const string TemplateName = "Room_Template";
    const string FallbackTile = "Assets/Tiles/Square.asset";

    const string ProjectilePrefab = "Assets/Prefabs/Projectile.prefab";

    // Paints (or erases, when paint is false) a block of tiles. x, y = bottom-left in world units.
    static void FillTiles(Tilemap map, TileBase tile, int x, int y, int width, int height, bool paint)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                map.SetTile(map.WorldToCell(new Vector3(x + i + 0.5f, y + j + 0.5f)), paint ? tile : null);
    }

    // A plain coloured box with a solid collider. Swap the sprite later for real art.
    static GameObject MakeBox(string name, Transform parent, Tilemap map, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (FirstTile(map) as Tile)?.sprite ?? AssetDatabase.LoadAssetAtPath<Tile>(FallbackTile)?.sprite;
        sr.color = color;
        sr.sortingOrder = 1;

        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    static void AddHealth(GameObject go, int maxHealth)
    {
        var health = go.AddComponent<Health>();
        var so = new SerializedObject(health);
        so.FindProperty("maxHealth").intValue = maxHealth;
        so.FindProperty("invincibleTime").floatValue = 0.1f; // same as the old Enemy, so fast punches count
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- LEVEL 1: THE SHIP ----------------
    // One tall scene, one screen wide. 4 floors + the top deck (boss arena, empty for now).
    // Each floor is exactly one screen tall, and the camera locks to the floor you are on.
    // Between two floors is a 2-tile divider (ceiling of the lower floor + floor of the upper one).
    // A hole in each divider, with a hook ring in it, takes you up. Holes switch sides: right, left, right, left.
    // A checkpoint is next to where you land.

    const string Level1Name = "Level_1";
    const float L1HalfHeight = 7.5f;          // camera zoomed out 1.5x compared to the tutorial
    const float L1FirstFloorCenterY = 0.5f;   // so floors line up with whole tiles
    const int WallLeft = -14, WallRight = 66;  // wall tile columns (inside is x -13 to 66, 3 screens wide)
    const int FloorSpacing = 15;               // one screen per floor
    const int Floors = 4;                   // floors 0..3. The ring in the top ceiling leads to the deck scene.
    const string DeckSceneName = "Level_1_Deck";
    const string SailSceneName = "Level_1_Sail";
    const string NextLevelName = "Level_2";
    const int DeckLeft = -14, DeckRight = 66; // deck railing columns: 3 screens long (inside x -13 to 66)
    const int FloorInside = 13;             // open space from floor to ceiling
    static readonly Color RingColor = new Color(1f, 0.85f, 0.1f);

    // Hazard damage in Level 1 (the player has 100 health). Change these and rebuild, or edit each object.
    const int TendrilDamage = 1;
    const int BarrelDamage = 20;
    const int CannonTouchDamage = 5;
    const int CannonBallDamage = 8;
    const string CannonBallPrefab = "Assets/Prefabs/CannonBall.prefab";
    static readonly Color CrateColor = new Color(0.6f, 0.4f, 0.2f);
    static readonly Color CannonColor = new Color(0.25f, 0.25f, 0.25f);
    static readonly Color CheckpointColor = new Color(0.9f, 0.9f, 0.1f);
}

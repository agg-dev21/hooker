using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static LevelKit;

// Editor-only helper. Adds Tools > Decorate > Level 2 Poison Tower.
// Oscar's level gets his look, like Hardy's ship has the goo: a damp, poisoned lizard den.
//   Dark green stone walls behind everything, moss on the floors, vines and poison drips hanging from the
//   ceilings, glowing poison mushrooms, bones, lizard claw marks, toxic fog in the pits, glowing green lanterns,
//   and a dimmer green light so the lanterns stand out.
// It reads the level's tiles to find the floors and ceilings, so nothing is placed inside walls.
// Decoration only: no colliders, gameplay doesn't change. Everything sits in one object, "PoisonDecor".
// Running it again replaces the old decoration. Tools > Decorations can switch it off and on.
public static class PoisonTowerDecorator
{
    const string ScenePath = "Assets/Scenes/Level_2.unity";
    const float GlobalLight = 0.6f;

    static readonly Color Stone = new Color(0.07f, 0.11f, 0.08f);
    static readonly Color StoneLine = new Color(0.04f, 0.07f, 0.05f);
    static readonly Color Moss = new Color(0.25f, 0.55f, 0.18f);
    static readonly Color MossDark = new Color(0.14f, 0.35f, 0.12f);
    static readonly Color Vine = new Color(0.18f, 0.42f, 0.15f);
    static readonly Color PoisonGreen = new Color(0.55f, 1f, 0.3f);
    static readonly Color Stem = new Color(0.75f, 0.78f, 0.6f);
    static readonly Color Bone = new Color(0.82f, 0.8f, 0.7f);
    static readonly Color Claw = new Color(0.02f, 0.04f, 0.03f);
    static readonly Color Fog = new Color(0.35f, 0.85f, 0.25f, 0.35f);
    static readonly Color LampGreen = new Color(0.55f, 1f, 0.45f);

    [MenuItem("Tools/Decorate/Level 2 Poison Tower")]
    static void Decorate()
    {
        if (!File.Exists(ScenePath) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Map = FindGroundTilemap(scene);
        BoxSprite = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");
        if (!Map) { EditorUtility.DisplayDialog("Decorate Level 2", "No ground tilemap in Level_2.", "OK"); return; }

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "PoisonDecor") Object.DestroyImmediate(r);
        var root = new GameObject("PoisonDecor").transform;
        var rng = new System.Random(2);

        // Which tiles are solid
        var solid = new HashSet<Vector2Int>();
        BoundsInt b = Map.cellBounds;
        foreach (Vector3Int c in b.allPositionsWithin)
            if (Map.HasTile(c)) solid.Add(new Vector2Int(c.x, c.y));
        bool Solid(int x, int y) => solid.Contains(new Vector2Int(x, y));

        // 1) Stone wall behind the tower and behind each hidden room
        Transform back = Group("BackWalls", root);
        BackWall(back, -1f, 40f, -1f, 65f);
        foreach (int floorY in new[] { 10, 28, 46 }) BackWall(back, -49f, -24f, floorY, floorY + 13);

        // 2) Floors and ceilings found from the tiles
        Transform growth = Group("Growth", root);
        int floors = 0, ceilings = 0;
        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                if (!Solid(x, y)) continue;
                double roll = rng.NextDouble();
                if (!Solid(x, y + 1)) // a floor surface at y + 1
                {
                    floors++;
                    float top = y + 1;
                    if (roll < 0.45) Deco("Moss", growth, new Vector2(x + 0.5f, top + 0.04f), new Vector2(1.02f, 0.14f), roll < 0.2 ? MossDark : Moss, 2);
                    if (roll < 0.12) Tuft(growth, new Vector2(x + 0.3f + (float)rng.NextDouble() * 0.4f, top), rng);
                    else if (roll < 0.17 && !Solid(x, y + 2)) Mushroom(growth, new Vector2(x + 0.5f, top), rng, floors % 3 == 0);
                    else if (roll < 0.19) BoneBit(growth, new Vector2(x + 0.5f, top + 0.08f), rng);
                }
                if (!Solid(x, y - 1)) // a ceiling underside at y
                {
                    ceilings++;
                    if (roll > 0.86) VineDown(growth, new Vector2(x + 0.5f, y), rng, Solid);
                    else if (roll > 0.8) Deco("PoisonDrip", growth, new Vector2(x + 0.5f, y - 0.18f), new Vector2(0.16f, 0.3f), PoisonGreen, 3);
                }
            }

        // 3) Lizard claw marks on the walls (in open air only)
        Transform marks = Group("ClawMarks", root);
        for (int i = 0; i < 18; i++)
        {
            float cx = (float)(rng.NextDouble() * 38 + 1), cy = (float)(rng.NextDouble() * 62 + 1);
            if (Solid(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy))) continue;
            for (int k = 0; k < 3; k++)
            {
                GameObject line = Deco("Claw", marks, new Vector2(cx + k * 0.28f, cy), new Vector2(0.07f, 1.2f), Claw, -17);
                line.transform.rotation = Quaternion.Euler(0f, 0f, -25f);
            }
        }

        // 4) Toxic fog in the bottom pits (gaps in the bottom floor)
        Transform fog = Group("Fog", root);
        for (int x = 0; x < 40; x++)
        {
            if (Solid(x, -1)) continue;
            int start = x;
            while (x < 40 && !Solid(x, -1)) x++;
            float w = x - start;
            Deco("ToxicFog", fog, new Vector2(start + w * 0.5f, -0.6f), new Vector2(w + 0.4f, 1.3f), Fog, 6);
            Deco("ToxicFogGlow", fog, new Vector2(start + w * 0.5f, -1.2f), new Vector2(w, 0.6f), new Color(Fog.r, Fog.g, Fog.b, 0.55f), 6);
        }

        // 5) Glowing green lanterns along the tower walls (only where there is open air)
        Transform lamps = Group("Lanterns", root);
        int lampCount = 0;
        for (int y = 4; y < 64; y += 9)
            foreach (float lx in new[] { 0.6f, 39.4f })
                if (!Solid(Mathf.FloorToInt(lx), y) && !Solid(Mathf.FloorToInt(lx), y + 1))
                {
                    Lantern("Lantern_" + lampCount++, lamps, new Vector2(lx, y), LampGreen);
                }

        // 6) Light: dimmer and greener so the lanterns and mushrooms glow
        foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { l.intensity = GlobalLight; l.color = new Color(0.8f, 1f, 0.82f); }
        Camera cam = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.05f, 0.04f); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        EditorUtility.DisplayDialog("Decorate Level 2",
            "Oscar's poison tower is decorated (" + floors + " floor spots, " + ceilings + " ceiling spots, " + lampCount + " lanterns).\n\n" +
            "Decoration only, no gameplay change. Switch it off with Tools > Decorations.", "OK");
    }

    // ---------------- Pieces ----------------

    static void BackWall(Transform parent, float x0, float x1, float y0, float y1)
    {
        Deco("Stone", parent, new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f), new Vector2(x1 - x0, y1 - y0), Stone, -20);
        for (float y = y0 + 1f; y < y1; y += 1.5f)
            Deco("StoneRow", parent, new Vector2((x0 + x1) * 0.5f, y), new Vector2(x1 - x0, 0.06f), StoneLine, -19);
    }

    static void Tuft(Transform parent, Vector2 bottom, System.Random rng)
    {
        for (int i = 0; i < 3; i++)
        {
            float h = 0.25f + (float)rng.NextDouble() * 0.25f;
            GameObject blade = Deco("Tuft", parent, bottom + new Vector2(i * 0.12f, h * 0.5f), new Vector2(0.06f, h), Moss, 3);
            blade.transform.rotation = Quaternion.Euler(0f, 0f, (i - 1) * 15f);
        }
    }

    // A poison mushroom; some glow
    static void Mushroom(Transform parent, Vector2 bottom, System.Random rng, bool glow)
    {
        float h = 0.25f + (float)rng.NextDouble() * 0.25f;
        Deco("MushroomStem", parent, bottom + new Vector2(0f, h * 0.5f), new Vector2(0.12f, h), Stem, 3);
        GameObject cap = Deco("MushroomCap", parent, bottom + new Vector2(0f, h + 0.08f), new Vector2(0.5f, 0.2f), PoisonGreen, 4);
        if (!glow) return;
        var light = new GameObject("Glow").AddComponent<Light2D>();
        light.transform.SetParent(cap.transform, false);
        light.lightType = Light2D.LightType.Point;
        light.color = PoisonGreen;
        light.intensity = 0.6f;
        light.pointLightOuterRadius = 2.2f;
        light.pointLightInnerRadius = 0.1f;
    }

    static void BoneBit(Transform parent, Vector2 at, System.Random rng)
    {
        GameObject bone = Deco("Bone", parent, at, new Vector2(0.6f, 0.1f), Bone, 3);
        bone.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 30f - 15f);
        Deco("BoneEnd", parent, at + new Vector2(-0.3f, 0f), new Vector2(0.16f, 0.16f), Bone, 3);
        Deco("BoneEnd", parent, at + new Vector2(0.3f, 0f), new Vector2(0.16f, 0.16f), Bone, 3);
    }

    // A vine hanging down from a ceiling, stopping before it reaches anything solid
    static void VineDown(Transform parent, Vector2 top, System.Random rng, System.Func<int, int, bool> solid)
    {
        float want = 1.2f + (float)rng.NextDouble() * 2.8f;
        int x = Mathf.FloorToInt(top.x);
        float length = 0f;
        while (length < want && !solid(x, Mathf.FloorToInt(top.y - length - 1f)) && !solid(x, Mathf.FloorToInt(top.y - length - 2f))) length += 1f;
        length = Mathf.Min(length, want);
        if (length < 0.8f) return;
        Deco("Vine", parent, top + new Vector2(0f, -length * 0.5f), new Vector2(0.1f, length), Vine, -4);
        for (float y = 0.4f; y < length; y += 0.6f)
            Deco("Leaf", parent, top + new Vector2((y % 1.2f < 0.6f) ? 0.12f : -0.12f, -y), new Vector2(0.18f, 0.12f), Moss, -4);
        Deco("VineDrop", parent, top + new Vector2(0f, -length - 0.12f), new Vector2(0.13f, 0.2f), PoisonGreen, -3);
    }
}

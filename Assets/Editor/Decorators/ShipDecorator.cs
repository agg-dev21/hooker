using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Makes Level 1 look like a real ship, in the same style as the Level 2 dungeon
// (layered decoration behind the playfield, flickering lights, dimmed global light).
// Themed around the Level 1 boss, Hardy (a Venom-style symbiote): the ship is infested with black goo
// with purple veins, dripping strands, cocoons and pulsing purple glows. The infestation gets heavier
// on each floor up, and the deck (his arena) is overrun.
// Decoration only: no colliders, nothing that changes gameplay. Running it again replaces the old decoration.
//   Tools > Other > Decorate > Level 1 Ship  - inside the ship: plank walls, ribs, beams, portholes, lanterns, ropes, cargo
//   Tools > Other > Decorate > Level 1 Deck  - the boss deck: night sky, moon, stars, sea, mast, sails, rigging, lanterns
public static class ShipDecorator
{
    const string ShipScene = "Assets/Scenes/Level_1.unity";
    const string DeckScene = "Assets/Scenes/Level_1_Deck.unity";
    const string SquareTile = "Assets/Tiles/Square.asset";

    // Level 1 layout (matches Tools > Build Level 1)
    const int Left = -14, Right = 66;      // outer wall columns
    const int Floors = 4;
    const int Spacing = 15, Inside = 13;   // floor to floor, open space inside a floor
    static int FloorTop(int k) => -6 + k * Spacing;
    const int RoomLeft = -50, RoomRight = -24; // secret rooms outside the ship
    const float DeckTop = -5f;             // deck surface (matches Tools > Rebuild Levels > Level 1 Deck)

    static readonly Color WoodBack = new Color(0.17f, 0.11f, 0.07f);
    static readonly Color PlankLine = new Color(0.1f, 0.06f, 0.04f);
    static readonly Color Beam = new Color(0.24f, 0.15f, 0.09f);
    static readonly Color BeamDark = new Color(0.14f, 0.09f, 0.05f);
    static readonly Color Brass = new Color(0.75f, 0.6f, 0.25f);
    static readonly Color SeaGlass = new Color(0.08f, 0.15f, 0.28f);
    static readonly Color Iron = new Color(0.25f, 0.25f, 0.28f);
    static readonly Color Rope = new Color(0.55f, 0.45f, 0.3f);
    static readonly Color LanternGlass = new Color(1f, 0.75f, 0.35f);
    static readonly Color Flame = new Color(1f, 0.6f, 0.15f);
    static readonly Color LampLight = new Color(1f, 0.65f, 0.35f);
    static readonly Color Moonlight = new Color(0.55f, 0.7f, 1f);
    static readonly Color Cargo = new Color(0.28f, 0.18f, 0.1f);
    static readonly Color CargoBand = new Color(0.18f, 0.12f, 0.07f);
    static readonly Color Goo = new Color(0.05f, 0.03f, 0.08f);
    static readonly Color GooShine = new Color(0.16f, 0.1f, 0.24f);
    static readonly Color Vein = new Color(0.5f, 0.18f, 0.85f);
    static readonly Color VenomGlow = new Color(0.6f, 0.25f, 1f);

    static Sprite box;

    // ---------------- Inside the ship ----------------

    [MenuItem("Tools/Other/Decorate/Level 1 Ship", priority = 3000)]
    static void DecorateShip()
    {
        if (!Open(ShipScene, out Scene scene)) return;
        Transform root = FreshRoot(scene, "ShipDecor");

        for (int k = 0; k < Floors; k++)
            DecorateFloor(k, Group("Floor_" + (k + 1), root));
        for (int k = 0; k < Floors; k++)
            DecorateSecretRoom(k, Group("SecretRoom_F" + (k + 1), root));

        SetLighting(0.42f, new Color(0.72f, 0.66f, 0.88f), new Color(0.03f, 0.02f, 0.04f));
        Save(scene, "Level 1 now looks like a ship infested by Hardy: plank walls, ribs, portholes, lanterns, and goo that spreads more on every floor up.");
    }

    static void DecorateFloor(int k, Transform parent)
    {
        float bottom = FloorTop(k), top = bottom + Inside, mid = (bottom + top) * 0.5f;
        float centerX = (Left + Right + 1) * 0.5f, width = Right - Left + 1;

        // Plank wall: horizontal planks with staggered seams
        Deco("BackWall", parent, new Vector2(centerX, mid), new Vector2(width, Inside + 2f), WoodBack, -20);
        for (int row = 0; row < Inside; row++)
        {
            float y = bottom + row;
            Deco("Plank", parent, new Vector2(centerX, y), new Vector2(width, 0.06f), PlankLine, -19);
            for (float x = Left + (row % 2 == 0 ? 2f : 6f); x < Right; x += 8f)
                Deco("Seam", parent, new Vector2(x, y + 0.5f), new Vector2(0.06f, 1f), PlankLine, -19);
        }

        // Ceiling beam and the ship's ribs with corner braces
        Deco("CeilingBeam", parent, new Vector2(centerX, top - 0.4f), new Vector2(width, 0.8f), BeamDark, -17);
        for (float x = -8f; x <= 62f; x += 10f)
        {
            Deco("Rib", parent, new Vector2(x, mid), new Vector2(0.9f, Inside), Beam, -18);
            Line("Brace", parent, new Vector2(x - 2f, top - 0.8f), new Vector2(x - 0.45f, top - 2.4f), 0.35f, Beam, -18);
            Line("Brace", parent, new Vector2(x + 2f, top - 0.8f), new Vector2(x + 0.45f, top - 2.4f), 0.35f, Beam, -18);
        }

        // Portholes between the ribs, with a little moonlight
        for (float x = -3f; x <= 57f; x += 20f)
        {
            Vector2 port = new Vector2(x + (k % 2) * 10f, bottom + 8f);
            Porthole(parent, port);
            if (k >= 1) GooPatch(parent, port + new Vector2(0.4f, 0.5f), 1.2f, k * 10 + (int)x); // goo creeping over it
        }

        // Hanging lanterns
        for (float x = 0f; x <= 60f; x += 15f)
            Lantern(parent, new Vector2(x + (k % 2) * 7f, top - 0.8f), 2.5f);

        // Ropes hanging from the beam, and background cargo along the walls
        for (float x = 4f; x <= 60f; x += 18f)
            Line("Rope", parent, new Vector2(x + k * 3f, top - 0.8f), new Vector2(x + k * 3f + 0.6f, top - 4.5f), 0.12f, Rope, -6);
        CargoStack(parent, new Vector2(Left + 2.5f, bottom), k);
        CargoStack(parent, new Vector2(Right - 2.5f, bottom), k + 1);

        Infest(parent, k, bottom, top);
    }

    // Hardy's goo spreading through the ship: more on every floor up (floor 1 = a little, floor 4 = overrun)
    static void Infest(Transform parent, int k, float bottom, float top)
    {
        var rng = new System.Random(100 + k);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        int patches = 3 + k * 4, drips = 2 + k * 4, cocoons = k, glows = 1 + k;
        for (int i = 0; i < patches; i++)
            GooPatch(parent, new Vector2(R(Left + 1f, Right - 1f), R(bottom + 1f, top - 1.5f)), R(1f, 1.8f + k * 0.5f), rng.Next());
        for (int i = 0; i < drips; i++)
            GooDrip(parent, new Vector2(R(Left + 1f, Right - 1f), top - 0.8f), R(1f, 2.5f + k));
        for (int i = 0; i < cocoons; i++)
            Cocoon(parent, new Vector2(R(Left + 3f, Right - 3f), top - 0.8f), R(1.5f, 3f));
        for (int i = 0; i < glows; i++)
            VenomHeart(parent, new Vector2(R(Left + 2f, Right - 2f), R(bottom + 3f, top - 3f)), 1.2f + k * 0.3f);
        // Goo pooling along the bottom of the walls
        for (int i = 0; i < 2 + k * 2; i++)
            Deco("GooPool", parent, new Vector2(R(Left + 1f, Right - 1f), bottom + 0.15f), new Vector2(R(1.5f, 4f), 0.3f), Goo, -9);
    }

    // The secret rooms outside the ship: same plank wall and a lantern
    static void DecorateSecretRoom(int k, Transform parent)
    {
        float bottom = FloorTop(k), top = bottom + Inside, mid = (bottom + top) * 0.5f;
        float centerX = (RoomLeft + RoomRight + 1) * 0.5f, width = RoomRight - RoomLeft + 1;
        Deco("BackWall", parent, new Vector2(centerX, mid), new Vector2(width, Inside + 2f), WoodBack, -20);
        for (int row = 0; row < Inside; row++)
            Deco("Plank", parent, new Vector2(centerX, bottom + row), new Vector2(width, 0.06f), PlankLine, -19);
        Deco("CeilingBeam", parent, new Vector2(centerX, top - 0.4f), new Vector2(width, 0.8f), BeamDark, -17);
        Lantern(parent, new Vector2(centerX, top - 0.8f), 3f);
        GooPatch(parent, new Vector2(RoomLeft + 4f, top - 3f), 1.5f, 40 + k);
        GooDrip(parent, new Vector2(RoomRight - 5f, top - 0.8f), 2f);
        CargoStack(parent, new Vector2(RoomLeft + 3f, bottom), k);
    }

    // ---------------- The deck ----------------

    [MenuItem("Tools/Other/Decorate/Level 1 Deck", priority = 3000)]
    static void DecorateDeck()
    {
        if (!Open(DeckScene, out Scene scene)) return;
        Transform root = FreshRoot(scene, "DeckDecor");
        float centerX = (Left + Right + 1) * 0.5f, width = Right - Left + 30f;

        // Night sky in bands, getting lighter toward the horizon
        Transform sky = Group("Sky", root);
        Color[] bands = { new Color(0.03f, 0.02f, 0.07f), new Color(0.06f, 0.03f, 0.11f), new Color(0.1f, 0.05f, 0.16f), new Color(0.16f, 0.08f, 0.22f) }; // purple-tinted night
        for (int i = 0; i < bands.Length; i++)
            Deco("SkyBand", sky, new Vector2(centerX, 30f - i * 8f), new Vector2(width, 8.2f), bands[i], -30);
        var rng = new System.Random(7);
        for (int i = 0; i < 160; i++)
        {
            float s = 0.06f + (float)rng.NextDouble() * 0.1f;
            Deco("Star", sky, new Vector2(Left - 10f + (float)rng.NextDouble() * (width), 2f + (float)rng.NextDouble() * 30f), new Vector2(s, s), new Color(1f, 1f, 0.9f, 0.6f + (float)rng.NextDouble() * 0.4f), -29);
        }
        GameObject moon = Deco("Moon", sky, new Vector2(40f, 8.5f), new Vector2(2.2f, 2.2f), new Color(0.9f, 0.82f, 0.95f), -28);
        Deco("MoonGlow", sky, new Vector2(40f, 8.5f), new Vector2(3.4f, 3.4f), new Color(0.9f, 0.9f, 1f, 0.12f), -29);
        AddLight(moon.transform, Moonlight, 0.6f, 25f, 3f);

        // Sea behind the railing, with a few distant islands
        Transform sea = Group("Sea", root);
        Deco("Sea", sea, new Vector2(centerX, DeckTop - 1f), new Vector2(width, 6f), new Color(0.05f, 0.1f, 0.2f), -27);
        for (float x = Left; x < Right; x += 3f)
            Deco("Wave", sea, new Vector2(x + 1f, DeckTop + 1.7f), new Vector2(1.2f, 0.05f), new Color(0.4f, 0.55f, 0.75f, 0.5f), -26);
        foreach (Vector3 isl in new[] { new Vector3(-4f, 6f, 1.2f), new Vector3(22f, 9f, 1.8f), new Vector3(55f, 5f, 1f) })
            Deco("Island", sea, new Vector2(isl.x, DeckTop + 2f + isl.z * 0.5f), new Vector2(isl.y, isl.z), new Color(0.03f, 0.05f, 0.08f), -26);

        // Mast, yards, sails and rigging
        Transform ship = Group("Rigging", root);
        foreach (float mx in new[] { 12f, 40f })
        {
            Deco("Mast", ship, new Vector2(mx, DeckTop + 18f), new Vector2(0.9f, 36f), BeamDark, -15);
            Deco("Yard", ship, new Vector2(mx, DeckTop + 13f), new Vector2(12f, 0.4f), BeamDark, -14);
            Deco("Sail", ship, new Vector2(mx, DeckTop + 9.5f), new Vector2(10.5f, 6.5f), new Color(0.1f, 0.07f, 0.13f), -16); // blackened by the goo
            Line("SailVein", ship, new Vector2(mx - 4f, DeckTop + 12f), new Vector2(mx + 1f, DeckTop + 7f), 0.08f, Vein, -15);
            Line("SailVein", ship, new Vector2(mx + 4.5f, DeckTop + 11.5f), new Vector2(mx - 0.5f, DeckTop + 8.5f), 0.06f, Vein, -15);
            Deco("SailTear", ship, new Vector2(mx + 2.5f, DeckTop + 7.2f), new Vector2(2f, 1.2f), new Color(0.06f, 0.03f, 0.11f), -15);
            GooPatch(ship, new Vector2(mx, DeckTop + 2.5f), 2.2f, (int)mx); // goo climbing the mast
            GooDrip(ship, new Vector2(mx - 3f, DeckTop + 13f), 2.5f);
            GooDrip(ship, new Vector2(mx + 3.5f, DeckTop + 13f), 3.5f);
            Deco("SailSeam", ship, new Vector2(mx, DeckTop + 9.5f), new Vector2(0.06f, 6.5f), new Color(0.55f, 0.5f, 0.4f), -15);
            Line("Shroud", ship, new Vector2(mx, DeckTop + 15f), new Vector2(mx - 9f, DeckTop + 1f), 0.08f, Rope, -14);
            Line("Shroud", ship, new Vector2(mx, DeckTop + 15f), new Vector2(mx + 9f, DeckTop + 1f), 0.08f, Rope, -14);
            Lantern(ship, new Vector2(mx + 0.6f, DeckTop + 6f), 0.8f);
        }
        Line("Stay", ship, new Vector2(12f, DeckTop + 20f), new Vector2(40f, DeckTop + 20f), 0.08f, Rope, -14);

        // Goo pooled along the deck and pulsing glows: this is Hardy's lair
        Transform lair = Group("Infestation", root);
        var gooRng = new System.Random(9);
        for (int i = 0; i < 10; i++)
            Deco("GooPool", lair, new Vector2(Left + 2f + (float)gooRng.NextDouble() * (Right - Left - 4), DeckTop + 0.15f), new Vector2(2f + (float)gooRng.NextDouble() * 4f, 0.3f), Goo, -9);
        foreach (float gx in new[] { 0f, 26f, 54f })
            VenomHeart(lair, new Vector2(gx, DeckTop + 3.5f), 1.8f);

        // Railing posts and top rail along the deck edge (behind the player)
        Transform rail = Group("Railing", root);
        Deco("TopRail", rail, new Vector2(centerX, DeckTop + 1.4f), new Vector2(Right - Left, 0.25f), Beam, -10);
        for (float x = Left + 1f; x < Right; x += 2f)
            Deco("Post", rail, new Vector2(x, DeckTop + 0.7f), new Vector2(0.2f, 1.4f), Beam, -10);

        // Cargo on the deck
        CargoStack(rail, new Vector2(Left + 4f, DeckTop), 0);
        CargoStack(rail, new Vector2(Right - 4f, DeckTop), 1);

        SetLighting(0.55f, new Color(0.68f, 0.6f, 0.9f), new Color(0.03f, 0.02f, 0.06f));
        Save(scene, "The deck is now Hardy's lair: purple-tinted night sky, sea, blackened sails, goo on the masts and deck, pulsing purple glows.");
    }

    // ---------------- Pieces ----------------

    static void Porthole(Transform parent, Vector2 c)
    {
        Deco("PortholeFrame", parent, c, new Vector2(1.7f, 1.7f), Brass, -17);
        GameObject glass = Deco("PortholeGlass", parent, c, new Vector2(1.25f, 1.25f), SeaGlass, -16);
        Deco("PortholeBar", parent, c, new Vector2(1.25f, 0.08f), Brass, -15);
        Deco("PortholeBar", parent, c, new Vector2(0.08f, 1.25f), Brass, -15);
        AddLight(glass.transform, Moonlight, 0.5f, 4f, 0.5f);
    }

    // A lantern hanging on a rope from (top). Its flame flickers (TorchFlicker) and lights the area.
    static void Lantern(Transform parent, Vector2 top, float ropeLength)
    {
        Vector2 body = top + Vector2.down * (ropeLength + 0.45f);
        Deco("LanternRope", parent, top + Vector2.down * ropeLength * 0.5f, new Vector2(0.06f, ropeLength), Rope, -6);
        Deco("LanternCap", parent, body + Vector2.up * 0.45f, new Vector2(0.55f, 0.15f), Iron, -4);
        Deco("LanternGlass", parent, body, new Vector2(0.45f, 0.7f), LanternGlass, -5);
        Deco("LanternBase", parent, body + Vector2.down * 0.4f, new Vector2(0.55f, 0.12f), Iron, -4);
        GameObject flame = Deco("LanternFlame", parent, body, new Vector2(0.18f, 0.6f), Flame, -3);
        Light2D light = AddLight(flame.transform, LampLight, 1.1f, 6.5f, 0.8f);
        var flicker = new SerializedObject(flame.AddComponent<TorchFlicker>());
        flicker.FindProperty("torchLight").objectReferenceValue = light;
        flicker.FindProperty("baseIntensity").floatValue = 1.1f;
        flicker.FindProperty("flicker").floatValue = 0.2f;
        flicker.ApplyModifiedPropertiesWithoutUndo();
    }

    // A blob of symbiote goo: a few overlapping, turned black shapes with purple veins
    static void GooPatch(Transform parent, Vector2 c, float size, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < 4; i++)
        {
            float s = size * (0.55f + (float)rng.NextDouble() * 0.5f);
            Vector2 offset = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * size * 0.7f;
            GameObject blob = Deco("Goo", parent, c + offset, new Vector2(s, s * 0.8f), i == 0 ? GooShine : Goo, -8);
            blob.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 90f);
        }
        for (int i = 0; i < 3; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector2 end = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * (0.7f + (float)rng.NextDouble() * 0.8f);
            Line("Vein", parent, c, end, 0.07f, Vein, -7);
        }
    }

    // A strand of goo hanging from the ceiling with a drop at the end
    static void GooDrip(Transform parent, Vector2 top, float length)
    {
        Deco("GooStrand", parent, top + Vector2.down * length * 0.5f, new Vector2(0.2f, length), Goo, -7);
        Deco("GooDrop", parent, top + Vector2.down * (length + 0.15f), new Vector2(0.38f, 0.45f), GooShine, -7);
    }

    // A dark cocoon hanging on a strand of goo
    static void Cocoon(Transform parent, Vector2 top, float strand)
    {
        Deco("CocoonStrand", parent, top + Vector2.down * strand * 0.5f, new Vector2(0.1f, strand), Goo, -7);
        Vector2 c = top + Vector2.down * (strand + 0.9f);
        GameObject body = Deco("Cocoon", parent, c, new Vector2(1f, 1.8f), Goo, -7);
        body.transform.rotation = Quaternion.Euler(0f, 0f, 8f);
        Line("CocoonVein", parent, c + new Vector2(-0.2f, 0.6f), c + new Vector2(0.15f, -0.6f), 0.06f, Vein, -6);
    }

    // A pulsing purple glow inside the goo (uses TorchFlicker for the pulse)
    static void VenomHeart(Transform parent, Vector2 c, float size)
    {
        GooPatch(parent, c, size, (int)(c.x * 13 + c.y * 7));
        Deco("VenomCore", parent, c, new Vector2(size * 0.25f, size * 0.25f), Vein, -6);
        // TorchFlicker also stretches the object it is on, so it goes on an invisible helper, not the core
        var pulser = new GameObject("VenomPulse");
        pulser.transform.SetParent(parent);
        pulser.transform.position = c;
        Light2D light = AddLight(pulser.transform, VenomGlow, 0.9f, 3f + size * 2f, 0.5f);
        var pulse = new SerializedObject(pulser.AddComponent<TorchFlicker>());
        pulse.FindProperty("torchLight").objectReferenceValue = light;
        pulse.FindProperty("baseIntensity").floatValue = 0.9f;
        pulse.FindProperty("flicker").floatValue = 0.35f;
        pulse.FindProperty("speed").floatValue = 1.5f; // slow throb, not a flame flicker
        pulse.ApplyModifiedPropertiesWithoutUndo();
    }

    // Two crates and a barrel standing on the floor (background only)
    static void CargoStack(Transform parent, Vector2 floor, int variant)
    {
        float dir = variant % 2 == 0 ? 1f : -1f;
        Crate(parent, floor + new Vector2(0f, 0.9f), 1.8f);
        Crate(parent, floor + new Vector2(dir * 1.9f, 0.7f), 1.4f);
        if (variant % 2 == 0) Crate(parent, floor + new Vector2(0.2f, 2.5f), 1.3f);
        Vector2 barrel = floor + new Vector2(-dir * 1.6f, 0.75f);
        Deco("Barrel", parent, barrel, new Vector2(1.1f, 1.5f), new Color(0.35f, 0.22f, 0.12f), -12);
        Deco("BarrelHoop", parent, barrel + Vector2.up * 0.45f, new Vector2(1.15f, 0.1f), Iron, -11);
        Deco("BarrelHoop", parent, barrel + Vector2.down * 0.45f, new Vector2(1.15f, 0.1f), Iron, -11);
    }

    static void Crate(Transform parent, Vector2 center, float size)
    {
        Deco("Crate", parent, center, new Vector2(size, size), Cargo, -12);
        Deco("CrateBand", parent, center, new Vector2(size, 0.12f), CargoBand, -11);
        Line("CrateBrace", parent, center + new Vector2(-size, -size) * 0.42f, center + new Vector2(size, size) * 0.42f, 0.12f, CargoBand, -11);
    }

    // ---------------- Helpers ----------------

    static bool Open(string path, out Scene scene)
    {
        scene = default;
        if (!System.IO.File.Exists(path))
        {
            EditorUtility.DisplayDialog("Decorate", path + " does not exist yet. Build it first.", "OK");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        box = AssetDatabase.LoadAssetAtPath<Tile>(SquareTile)?.sprite;
        return true;
    }

    static void Save(Scene scene, string message)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Decorate", message + "\n\nRun it again any time: it replaces the old decoration.", "OK");
    }

    // Removes old decoration with this name, then makes a new empty group
    static Transform FreshRoot(Scene scene, string name)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == name) Object.DestroyImmediate(r);
        return new GameObject(name).transform;
    }

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    // Decoration only: a coloured box with no collider
    static GameObject Deco(string name, Transform parent, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = box;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    // A thin bar from a to b (ropes, braces)
    static GameObject Line(string name, Transform parent, Vector2 a, Vector2 b, float thickness, Color color, int order)
    {
        Vector2 d = b - a;
        GameObject go = Deco(name, parent, (a + b) * 0.5f, new Vector2(d.magnitude, thickness), color, order);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return go;
    }

    static Light2D AddLight(Transform parent, Color color, float intensity, float outer, float inner)
    {
        var light = new GameObject("Light").AddComponent<Light2D>();
        light.transform.SetParent(parent, false);
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = outer;
        light.pointLightInnerRadius = inner;
        return light;
    }

    // Dim the scene's global light so the lanterns stand out, and set the camera's empty-space colour
    static void SetLighting(float intensity, Color color, Color background)
    {
        foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            if (l.lightType == Light2D.LightType.Global)
            {
                l.intensity = intensity;
                l.color = color;
            }
        Camera cam = Object.FindAnyObjectByType<Camera>();
        if (cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
        }
    }
}

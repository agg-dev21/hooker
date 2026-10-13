using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Part of TutorialRoomBuilder (see TutorialRoomBuilder.cs for its settings and the main loop).
// Scene helpers shared by the builders in this class (prompts, colliders, tilemap lookups).
public static partial class TutorialRoomBuilder
{
    // Makes the tilemap's collider match the painted tiles (it does not update by itself in the Editor).
    // Returns true if the Composite (merge) collider would not update and was removed,
    // so the plain Tilemap Collider 2D is used instead.
    static bool RebuildColliders(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        if (!tilemap.TryGetComponent(out TilemapCollider2D tc)) return false;

        // Switch off and on to force a rebuild
        tc.enabled = false;
        tc.enabled = true;
        tc.ProcessTilemapChanges();

        if (!tilemap.TryGetComponent(out CompositeCollider2D composite)) return false;
        composite.enabled = false;
        composite.enabled = true;
        composite.GenerateGeometry();

        // Does the merged shape now cover the same area as the tiles?
        Bounds tiles = tilemap.localBounds;
        tiles.center = tilemap.transform.TransformPoint(tiles.center);
        Bounds shape = composite.bounds;
        bool matches = Mathf.Abs(shape.min.x - tiles.min.x) < 0.1f && Mathf.Abs(shape.max.x - tiles.max.x) < 0.1f &&
                       Mathf.Abs(shape.min.y - tiles.min.y) < 0.1f && Mathf.Abs(shape.max.y - tiles.max.y) < 0.1f;
        if (matches) return false;

        // Still the old shape: stop merging and remove the Composite, so the tiles' own colliders are used
        tc.compositeOperation = Collider2D.CompositeOperation.None;
        Object.DestroyImmediate(composite);
        return true;
    }

    // ---------------- "Press E" pop-ups ----------------
    // Puts a "Press E" text just above the object and links it to the component's Prompt slot.
    // The text is a sibling (not a child) so it does not get stretched by the object's scale.
    static bool AddPrompt(GameObject target, Component owner)
    {
        var so = new SerializedObject(owner);
        var promptProp = so.FindProperty("prompt");
        if (promptProp == null || promptProp.objectReferenceValue) return false;

        var go = new GameObject(target.name + "_PressE");
        go.transform.SetParent(target.transform.parent, false);
        var bounds = target.GetComponent<SpriteRenderer>() ? target.GetComponent<SpriteRenderer>().bounds : new Bounds(target.transform.position, Vector3.one);
        go.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.6f, 0f);
        var text = go.AddComponent<TMPro.TextMeshPro>();
        text.text = "Press E";
        text.fontSize = 4;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        text.GetComponent<MeshRenderer>().sortingOrder = 20;
        go.SetActive(false);

        promptProp.objectReferenceValue = go;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static string ScenePath(string name) => $"{ScenesFolder}/{name}.unity";

    static void DestroyByName(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t && t.name == name) Object.DestroyImmediate(t.gameObject);
    }

    static Tilemap FindGroundTilemap()
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        foreach (Tilemap map in maps)
            if (map.gameObject.layer == ground) return map;
        return maps[0];
    }

    static TileBase FirstTile(Tilemap tilemap)
    {
        foreach (TileBase t in tilemap.GetTilesBlock(tilemap.cellBounds))
            if (t) return t;
        return null;
    }
}

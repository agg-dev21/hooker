using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Part of PoisonLevelBuilder (see PoisonLevelBuilder.cs for its settings and the main loop).
// Low-level helpers: groups, tiles, boxes, prompts, colliders, tilemap lookups.
public static partial class PoisonLevelBuilder
{
    // ---------------- Helpers ----------------

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    // Paints (or erases) a block of tiles. x, y = bottom-left tile.
    static void Fill(int x, int y, int width, int height, bool paint = true)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                map.SetTile(map.WorldToCell(new Vector3(x + i + 0.5f, y + lift + j + 0.5f)), paint ? tile : null);
    }

    // A plain coloured box with a solid collider. Swap the sprite later for real art.
    static GameObject Box(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center + Vector2.up * lift;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = color;
        sr.sortingOrder = 1;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    static void AddHealth(GameObject go, int maxHealth)
    {
        var so = new SerializedObject(go.AddComponent<Health>());
        so.FindProperty("maxHealth").intValue = maxHealth;
        so.FindProperty("invincibleTime").floatValue = 0.1f; // so fast punches count
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // "Press E" pop-up above a door or chest (hidden until the player is near)
    static void AddPrompt(GameObject target, Component owner)
    {
        var so = new SerializedObject(owner);
        var promptProp = so.FindProperty("prompt");
        if (promptProp == null) return;

        var go = new GameObject(target.name + "_PressE");
        go.transform.SetParent(target.transform.parent, false);
        Bounds b = target.GetComponent<SpriteRenderer>().bounds;
        go.transform.position = new Vector3(b.center.x, b.max.y + 0.6f, 0f);
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
    }

    static void RebuildColliders(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        if (!tilemap.TryGetComponent(out TilemapCollider2D tc)) return;
        tc.enabled = false;
        tc.enabled = true;
        tc.ProcessTilemapChanges();
        if (!tilemap.TryGetComponent(out CompositeCollider2D composite)) return;
        composite.enabled = false;
        composite.enabled = true;
        composite.GenerateGeometry();
    }

    static void DestroyByName(Scene scene, string name)
    {
        var doomed = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) doomed.Add(t.gameObject);
        foreach (GameObject go in doomed) if (go) Object.DestroyImmediate(go);
    }

    static Tilemap FindGroundTilemap(Scene scene)
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap first = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Tilemap m in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (m.gameObject.layer == ground) return m;
                if (!first) first = m;
            }
        return first;
    }

    static TileBase FirstTile(Tilemap tilemap)
    {
        foreach (TileBase t in tilemap.GetTilesBlock(tilemap.cellBounds))
            if (t) return t;
        return null;
    }
}

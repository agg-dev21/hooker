using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Editor-only helpers for items (Tools > Items):
//   Item Settings          - select the settings asset to change every item's numbers
//   Add Item Pickup Here   - drops an item pickup in the open scene, in the middle of the Scene view
//   Give One Of Each       - testing: puts one of every item in the inventory
//   Clear Inventory        - testing: empties the inventory
public static class ItemTools
{
    const string SquareTile = "Assets/Tiles/Square.asset";

    [MenuItem("Tools/Other/Items/Item Settings", priority = 3000)]
    static void SelectSettings()
    {
        var settings = Resources.Load<ItemSettings>("ItemSettings");
        if (settings)
        {
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
        else EditorUtility.DisplayDialog("Items", "Assets/Resources/ItemSettings.asset is missing.", "OK");
    }

    [MenuItem("Tools/Other/Items/Add Item Pickup Here", priority = 3000)]
    static void AddPickup()
    {
        Vector3 pos = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var go = new GameObject("ItemPickup");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = Vector3.one * 0.7f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Tile>(SquareTile)?.sprite;
        sr.sortingOrder = 5;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.7f;
        go.AddComponent<ItemPickup>();
        Undo.RegisterCreatedObjectUndo(go, "Add Item Pickup");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("Tools/Other/Items/Give One Of Each (testing)", priority = 3000)]
    static void GiveAll()
    {
        foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            Inventory.Add(t, 1, "Test scroll", "This is a test note.\nType the real hint about the mother on the scroll's chest or pickup.");
        Inventory.Reload();
        EditorUtility.DisplayDialog("Items", "One of every item is now in the inventory. Press Play to try them (Tab / 1-8 to pick, F to use).", "OK");
    }

    [MenuItem("Tools/Other/Items/Clear Inventory", priority = 3000)]
    static void ClearAll()
    {
        Inventory.Clear();
        Inventory.Reload();
        EditorUtility.DisplayDialog("Items", "The inventory is empty.", "OK");
    }
}

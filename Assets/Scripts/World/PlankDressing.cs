using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Dresses each level with its planks (Resources/PlankArt), following the level's own shape, never at random:
//   - every walkable top of the ground (a tile with nothing above it) gets a plank strip along its whole width
//   - every thin jump-through platform is drawn as a plank (its own box is hidden)
//   - floating platforms get a support post at each end, down to the floor below (only sets that have a post)
// Only the look changes: no colliders are added or moved. Adds itself to every scene, nothing to set up.
public static class PlankDressing
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlankArt art = PlankArt.Current;
        if (!art || !art.dressingOn) return;
        PlankArt.Set set = art.ForScene(scene.name);
        if (set == null || !set.floor) return;

        Tilemap map = GroundTilemap(scene);
        if (!map) return;
        var cells = new HashSet<Vector2Int>();
        BoundsInt b = map.cellBounds;
        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
                if (map.HasTile(new Vector3Int(x, y, 0))) cells.Add(new Vector2Int(x, y));
        if (cells.Count == 0) return;

        int order = map.TryGetComponent(out TilemapRenderer tr) ? tr.sortingOrder + 1 : 1;
        int layer = tr ? tr.sortingLayerID : 0;
        var root = new GameObject("Planks").transform;

        FloorStrips(map, cells, b, set.floor, root, order, layer);
        Platforms(map, cells, set, art.maxPostLength, root, order, layer);
    }

    // Plank strips on top of every run of tiles that has open space above it
    static void FloorStrips(Tilemap map, HashSet<Vector2Int> cells, BoundsInt b, Sprite plank, Transform root, int order, int layer)
    {
        Vector3 cell = map.cellSize;
        for (int y = b.yMin; y < b.yMax; y++)
        {
            int x = b.xMin;
            while (x < b.xMax)
            {
                if (!IsTop(cells, x, y)) { x++; continue; }
                int start = x;
                while (x < b.xMax && IsTop(cells, x, y)) x++;
                Vector3 left = map.CellToWorld(new Vector3Int(start, y + 1, 0)); // top-left corner of the run
                float width = (x - start) * cell.x;
                Strip("Floor", root, new Vector2(left.x + width * 0.5f, left.y), width, plank, order, layer);
            }
        }
    }

    static bool IsTop(HashSet<Vector2Int> cells, int x, int y) =>
        cells.Contains(new Vector2Int(x, y)) && !cells.Contains(new Vector2Int(x, y + 1));

    // The thin jump-through platforms (one-way PlatformEffector2D) become planks, with posts under them
    static void Platforms(Tilemap map, HashSet<Vector2Int> cells, PlankArt.Set set, float maxPost, Transform root, int order, int layer)
    {
        Sprite plank = set.platform ? set.platform : set.floor;
        foreach (PlatformEffector2D e in Object.FindObjectsByType<PlatformEffector2D>(FindObjectsInactive.Exclude))
        {
            if (!e.useOneWay || !e.TryGetComponent(out Collider2D col)) continue;
            Bounds pb = col.bounds;
            Strip("Platform", root, new Vector2(pb.center.x, pb.max.y), pb.size.x, plank, order + 1, layer);
            if (e.TryGetComponent(out SpriteRenderer box)) box.enabled = false;

            if (!set.post) continue;
            float inset = Mathf.Min(0.6f, pb.size.x * 0.25f);
            foreach (float px in new[] { pb.min.x + inset, pb.max.x - inset })
            {
                float floorY = FloorBelow(map, cells, new Vector2(px, pb.min.y), maxPost);
                if (float.IsNaN(floorY)) continue;
                Post(root, px, pb.min.y, floorY, set.post, order, layer);
            }
        }
    }

    // Top of the first ground tile under this point, or NaN if there is none close enough
    static float FloorBelow(Tilemap map, HashSet<Vector2Int> cells, Vector2 from, float maxDistance)
    {
        Vector3Int c = map.WorldToCell(from);
        for (int y = c.y - 1; y >= c.y - Mathf.CeilToInt(maxDistance / Mathf.Max(map.cellSize.y, 0.01f)); y--)
            if (cells.Contains(new Vector2Int(c.x, y)))
            {
                float top = map.CellToWorld(new Vector3Int(c.x, y + 1, 0)).y;
                return from.y - top >= 0.5f ? top : float.NaN; // too short to bother
            }
        return float.NaN;
    }

    // A plank strip whose top edge is at "top", repeated to fill this width
    static void Strip(string name, Transform root, Vector2 top, float width, Sprite sprite, int order, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = top; // the picture's pivot is its top middle
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, sprite.bounds.size.y);
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
    }

    // A post standing on the floor, reaching up to the platform
    static void Post(Transform root, float x, float topY, float floorY, Sprite sprite, int order, int layer)
    {
        var go = new GameObject("Post");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector2(x, topY); // the post's pivot is its top middle
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(sprite.bounds.size.x, topY - floorY);
        sr.sortingLayerID = layer;
        sr.sortingOrder = order - 2; // behind the planks and the ground
    }

    // The level's solid ground: the tilemap on the Ground layer, otherwise the biggest one with a collider
    static Tilemap GroundTilemap(Scene scene)
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap best = null;
        int bestCount = -1;
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Tilemap m in r.GetComponentsInChildren<Tilemap>())
            {
                if (m.gameObject.layer == ground && m.GetComponent<TilemapCollider2D>()) return m;
                if (!m.GetComponent<TilemapCollider2D>()) continue;
                int n = m.GetUsedTilesCount();
                if (n > bestCount) { best = m; bestCount = n; }
            }
        return best;
    }
}

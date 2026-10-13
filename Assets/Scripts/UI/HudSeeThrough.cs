using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Keeps the HUD from hiding the game: when something in the level is behind a piece of the HUD
// (the floor, a door, a ledge, an enemy, the player...), that piece fades out so you can see through it.
// When nothing is behind it, it fades back in. Nothing is moved or removed, it only gets see-through.
// HUD pieces it looks after: the bosses' health bars, the item bar (1-4),
// and the ability / chain / companion icons. Menus, dialogue and the inventory screen are never faded.
// Installs itself; nothing needs to be set up in the scenes.
public class HudSeeThrough : MonoBehaviour
{
    const float SeeThroughAlpha = 0.3f; // how visible a HUD piece stays when something is behind it
    const float FadeSpeed = 6f;
    const float CheckEvery = 0.1f;      // seconds between checks
    const float Inset = 0.12f;          // ignore the outer 12% of each piece (a wall just touching its edge doesn't count)

    static readonly string[] IconNames = { "AbilityIcon", "ChainAbilityIcon", "CompanionIcon" };

    class Piece
    {
        public GameObject root;
        public CanvasGroup group;
        public float target = 1f;
    }

    readonly List<Piece> pieces = new List<Piece>();
    readonly Vector3[] corners = new Vector3[4];
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("HudSeeThrough");
        DontDestroyOnLoad(go);
        go.AddComponent<HudSeeThrough>();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckEvery;
            FindPieces();
            foreach (Piece p in pieces) p.target = SomethingBehind(p.root) ? SeeThroughAlpha : 1f;
        }
        foreach (Piece p in pieces)
            if (p.group) p.group.alpha = Mathf.MoveTowards(p.group.alpha, p.target, FadeSpeed * Time.unscaledDeltaTime);
    }

    // ---------------- Which pieces ----------------

    void FindPieces()
    {
        pieces.RemoveAll(p => !p.root);

        // Boss bars only: the player's bar hides itself when it isn't needed (HealthBar)
        foreach (HealthBar bar in FindObjectsByType<HealthBar>(FindObjectsInactive.Exclude))
            if (!bar.IsPlayerBar) Add(bar.gameObject);
        foreach (string name in IconNames)
        {
            GameObject icon = GameObject.Find(name);
            if (icon) Add(icon);
        }
        GameObject inventory = GameObject.Find("InventoryUI");
        if (inventory)
        {
            Transform itemBar = inventory.transform.Find("Bar") ?? FindDeep(inventory.transform, "Bar");
            if (itemBar) Add(itemBar.gameObject);
        }
    }

    void Add(GameObject go)
    {
        foreach (Piece p in pieces) if (p.root == go) return;
        var group = go.GetComponent<CanvasGroup>();
        if (!group) group = go.AddComponent<CanvasGroup>();
        pieces.Add(new Piece { root = go, group = group });
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // ---------------- Is something behind it? ----------------

    bool SomethingBehind(GameObject root)
    {
        Camera cam = Camera.main;
        if (!cam || !root.activeInHierarchy) return false;

        // The piece's area on screen (all its visible images and texts together)
        bool any = false;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Graphic g in root.GetComponentsInChildren<Graphic>())
        {
            if (!g.enabled || g.color.a <= 0.01f) continue;
            Canvas canvas = g.canvas;
            Camera uiCam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            g.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 c in corners)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(uiCam, c);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
                any = true;
            }
        }
        if (!any) return false;

        // The same area in the level, and whatever visible thing is there
        Vector2 inset = (max - min) * Inset;
        Vector2 a = cam.ScreenToWorldPoint(new Vector3(min.x + inset.x, min.y + inset.y, 0f));
        Vector2 b = cam.ScreenToWorldPoint(new Vector3(max.x - inset.x, max.y - inset.y, 0f));
        foreach (Collider2D col in Physics2D.OverlapAreaAll(a, b))
            if (IsVisible(col)) return true;
        return false;
    }

    // Only things you can actually see count (not invisible kill zones, camera edges, trigger areas...)
    static bool IsVisible(Collider2D col)
    {
        foreach (Renderer r in col.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.isVisible) return true;
        Renderer own = col.GetComponentInParent<Renderer>();
        return own && own.enabled && own.isVisible;
    }
}
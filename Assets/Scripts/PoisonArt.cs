using UnityEngine;

// The poison drawings used everywhere in the game (Assets/Resources/PoisonArt.asset).
// Made and filled by Tools > Art > Poison. Select the asset to change sizes and speeds.
//   Drop      - falling drops (drippers, the lizard's rain) and the lizard's vomit globs
//   Splash    - plays once where poison lands
//   Puddle    - poison puddles and poison floors (repeats along the whole width)
//   Drips     - rising poison bits: on slime walls, and on anyone who is poisoned
// Every PoisonHazard dresses itself with these when the game runs (see Dress below).
// If the asset is missing, everything keeps its old plain green look.
[CreateAssetMenu(fileName = "PoisonArt", menuName = "Hooker/Poison Art")]
public class PoisonArt : ScriptableObject
{
    [Header("Drawings (filled by Tools > Art > Poison)")]
    public Sprite drop;
    public Sprite[] splash;
    public Sprite puddle;
    public Sprite[] drips;

    [Header("Where to use them")]
    public bool dressDrops = true;
    public bool dressPuddles = true;
    public bool dressSlimeWalls = true;
    public bool showWhenPoisoned = true;   // rising drips on the player or an enemy while poisoned
    public bool splashWhenDropsLand = true;

    [Header("Sizes")]
    public float dropSize = 1.4f;          // drop drawing size compared to the drop's hit circle
    public float puddleHeight = 0.6f;      // how tall a puddle drawing is (world units)
    public float slimeWallWidth = 1.2f;    // how wide the drips on a slime wall are
    public float splashSize = 1.5f;        // splash width compared to the drop that made it
    public float poisonedWidth = 1.3f;     // poisoned effect width compared to the target's body
    [Range(0f, 1f)] public float poisonedAlpha = 0.85f;

    [Header("Speeds (frames per second)")]
    public float splashFps = 10f;
    public float dripFps = 6f;

    // ---------------- Finding the asset ----------------

    static PoisonArt current;
    static bool loaded;

    public static PoisonArt Current
    {
        get
        {
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<PoisonArt>("PoisonArt");
                loaded = true;
            }
            return current;
        }
    }

    const string ArtChildName = "PoisonArt";

    // ---------------- Hazards ----------------

    // Swaps a poison hazard's plain green box for the drawing that fits its shape:
    // round (drops, globs) = drop, wide and flat = puddle, tall and thin = slime wall.
    // Skips spiked balls and enemies (anything with Health), and anything already dressed or hidden.
    public static void Dress(GameObject go)
    {
        PoisonArt art = Current;
        if (!art || !go) return;
        if (!go.TryGetComponent(out SpriteRenderer box) || !box.enabled) return;
        if (go.transform.Find(ArtChildName)) return;
        if (go.GetComponentInParent<Health>() || go.GetComponentInParent<PoisonPendulum>()) return;

        Vector3 ls = go.transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(ls.x), 0.0001f);
        float sy = Mathf.Max(Mathf.Abs(ls.y), 0.0001f);
        bool round = go.GetComponent<PoisonDrop>() || go.GetComponent<CircleCollider2D>();

        if (round)
        {
            if (!art.dressDrops || !art.drop) return;
            Vector2 drawn = art.drop.bounds.size;
            SpriteRenderer sr = MakeChild(go, box, art.drop);
            sr.transform.localScale = Vector3.one * (art.dropSize / Mathf.Max(drawn.x, drawn.y, 0.01f));
            box.enabled = false;
        }
        else if (sx >= sy * 2f)
        {
            // Puddle: lies on the bottom edge of the box and repeats along its width
            if (!art.dressPuddles || !art.puddle) return;
            Vector2 drawn = art.puddle.bounds.size;
            float k = art.puddleHeight / Mathf.Max(drawn.y, 0.01f); // drawing scale in world units
            SpriteRenderer sr = MakeChild(go, box, art.puddle);
            sr.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            sr.transform.localScale = new Vector3(k / sx, k / sy, 1f);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Adaptive;
            sr.size = new Vector2(sx / k, drawn.y);
            box.enabled = false;
        }
        else if (sy >= sx * 2f)
        {
            // Slime wall: the green strip stays, rising drips repeat along its height
            if (!art.dressSlimeWalls || art.drips == null || art.drips.Length == 0) return;
            Vector2 drawn = art.drips[0].bounds.size;
            float k = art.slimeWallWidth / Mathf.Max(drawn.x, 0.01f);
            SpriteRenderer sr = MakeChild(go, box, art.drips[0]);
            sr.transform.localScale = new Vector3(k / sx, k / sy, 1f);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Adaptive;
            sr.size = new Vector2(drawn.x, sy / k);
            SpriteLoop.Add(sr.gameObject, art.drips, art.dripFps, true);
        }
    }

    static SpriteRenderer MakeChild(GameObject parent, SpriteRenderer box, Sprite sprite)
    {
        var child = new GameObject(ArtChildName);
        child.transform.SetParent(parent.transform, false);
        var sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = box.sortingLayerID;
        sr.sortingOrder = box.sortingOrder + 1;
        return sr;
    }

    // ---------------- Splash ----------------

    // Plays the splash once, standing on the point "at", about "width" world units wide
    public static void SplashAt(Vector2 at, float width)
    {
        PoisonArt art = Current;
        if (!art || art.splash == null || art.splash.Length == 0) return;
        var fx = new GameObject("PoisonSplash");
        fx.transform.position = at;
        float drawnWidth = Mathf.Max(art.splash[0].bounds.size.x, 0.01f);
        fx.transform.localScale = Vector3.one * (Mathf.Max(width, 0.1f) / drawnWidth);
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = art.splash[0];
        sr.sortingOrder = 4;
        SpriteLoop.Add(fx, art.splash, art.splashFps, false).destroyWhenDone = true;
    }

    // ---------------- Poisoned effect ----------------

    // Rising drips over a poisoned target. Returns the effect object (null if turned off).
    public static GameObject AddPoisonedEffect(GameObject target)
    {
        PoisonArt art = Current;
        if (!art || !art.showWhenPoisoned || art.drips == null || art.drips.Length == 0 || !target) return null;

        // The target's body: a solid collider first, then any collider, then its drawing
        Bounds body = new Bounds(target.transform.position, Vector3.one);
        bool found = false;
        foreach (Collider2D c in target.GetComponents<Collider2D>())
            if (c.enabled && !c.isTrigger) { body = c.bounds; found = true; break; }
        if (!found && target.TryGetComponent(out Collider2D any)) { body = any.bounds; found = true; }
        if (!found && target.TryGetComponent(out Renderer r)) body = r.bounds;

        Vector2 drawn = art.drips[0].bounds.size;
        float k = Mathf.Max(body.size.x * art.poisonedWidth, 0.8f) / Mathf.Max(drawn.x, 0.01f);
        Vector3 ls = target.transform.lossyScale;

        var fx = new GameObject("PoisonedEffect");
        fx.transform.SetParent(target.transform, false);
        fx.transform.position = body.center;
        fx.transform.localScale = new Vector3(k / NonZero(ls.x), k / NonZero(ls.y), 1f);
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = art.drips[0];
        sr.color = new Color(1f, 1f, 1f, art.poisonedAlpha);
        sr.sortingOrder = 20;
        SpriteLoop.Add(fx, art.drips, art.dripFps, true);
        return fx;
    }

    static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 0.0001f : v;
}

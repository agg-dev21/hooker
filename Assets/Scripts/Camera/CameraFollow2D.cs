using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    // A horizontal band of the level (a floor). The camera never shows above Top or below Bottom
    // while the player is in it.
    [System.Serializable]
    public struct CameraZone
    {
        public float bottom; // lowest point the camera may show (include the floor tiles you stand on)
        public float top;    // highest point the camera may show
        public float size;   // camera size (zoom) in this zone; 0 = the camera's normal size
    }

    public static CameraFollow2D Instance { get; private set; }

    [SerializeField] Transform target;
    [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);
    [SerializeField] float smoothTime = 0.15f;

    [Header("Look Ahead")]
    [SerializeField] float lookAheadDistance = 2.5f;
    [SerializeField] float lookAheadSmooth = 0.4f;

    [Header("Vertical")]
    [SerializeField] float verticalDeadZone = 1.5f;

    [Header("Level Bounds (optional)")]
    [SerializeField] bool useBounds;
    [SerializeField] Vector2 minBounds;
    [SerializeField] Vector2 maxBounds;

    [Header("Floors (optional)")]
    [SerializeField] bool lockToFloors;            // ON: the camera only shows the floor the player is on
    [SerializeField] float floorHeight = 10f;       // height of one floor (one screen)
    [SerializeField] float firstFloorCenterY = 0f;  // center of the bottom floor

    [Header("Zones (optional, for floors of different heights)")]
    [SerializeField] CameraZone[] zones; // bottom to top. The player is in the highest zone whose Bottom + 1 is below them.
    [SerializeField] float zoomSpeed = 6f; // how fast the camera zooms when a zone has its own size

    [Header("Hook rings")]
    [SerializeField] bool showNearbyRings = true; // keep the next ring that takes you UP on screen (moving / zooming out a little)
    [SerializeField] float ringMargin = 1.2f;     // space kept between the ring and the screen edge

    Camera cam;
    PlayerController2D player;
    Vector3 velocity;
    float currentLookAhead;
    float lookAheadVel;
    float trackedY;
    float shakeTime;
    float shakeMagnitude;
    bool hasOverride;       // a secret room (or similar) is showing its own area
    Vector2 overrideMin;
    Vector2 overrideMax;
    bool snapNext;          // jump straight to the target next frame instead of sliding
    float normalSize;       // the camera's size when no zone changes it

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        normalSize = cam.orthographicSize * Zoom; // a little closer than the scene says (PlayerBalance)
        cam.orthographicSize = normalSize;
        if (target) player = target.GetComponent<PlayerController2D>();
        if (target) trackedY = target.position.y;
    }

    void LateUpdate()
    {
        if (!target) return;

        float lookTarget = player ? player.FacingDirection * lookAheadDistance : 0f;
        currentLookAhead = Mathf.SmoothDamp(currentLookAhead, lookTarget, ref lookAheadVel, lookAheadSmooth);

        float ty = target.position.y;
        bool grounded = player == null || player.IsGrounded;
        if (grounded || Mathf.Abs(ty - trackedY) > verticalDeadZone)
            trackedY = grounded ? ty : ty - Mathf.Sign(ty - trackedY) * verticalDeadZone;

        Vector3 desired = new Vector3(
            target.position.x + offset.x + currentLookAhead,
            trackedY + offset.y,
            transform.position.z);

        // Floor lock: center the camera on the player's floor so the floors above and below are hidden
        if (lockToFloors && floorHeight > 0f && !hasOverride)
        {
            float bottom = firstFloorCenterY - floorHeight * 0.5f;
            int floor = Mathf.FloorToInt((target.position.y - bottom) / floorHeight);
            float floorCenter = firstFloorCenterY + floor * floorHeight;
            // Zoomed in closer than one floor: follow the player but never show the next floor
            float room = floorHeight * 0.5f - cam.orthographicSize;
            desired.y = room > 0f ? Mathf.Clamp(desired.y, floorCenter - room, floorCenter + room) : floorCenter;
        }

        // Zones: keep the camera inside the player's floor, so the floors above and below stay hidden
        float wantedSize = normalSize;
        if (zones != null && zones.Length > 0 && !hasOverride)
        {
            CameraZone zone = zones[ZoneAt(target.position.y)];
            if (zone.size > 0f) wantedSize = zone.size * Zoom;
            float halfH = wantedSize;
            desired.y = zone.top - zone.bottom <= halfH * 2f
                ? (zone.bottom + zone.top) * 0.5f
                : Mathf.Clamp(desired.y, zone.bottom + halfH, zone.top - halfH);
        }

        // Hook rings: if there is a ring up ahead, frame the camera so you can see it (and still see yourself)
        if (showNearbyRings && !hasOverride && (lockToFloors || zones == null || zones.Length == 0))
            FrameNearbyRing(ref desired, ref wantedSize);

        // Zoom toward the zone's size (or back to normal)
        if (!Mathf.Approximately(cam.orthographicSize, wantedSize))
            cam.orthographicSize = snapNext ? wantedSize : Mathf.MoveTowards(cam.orthographicSize, wantedSize, zoomSpeed * Time.deltaTime);

        Vector3 pos;
        if (snapNext)
        {
            pos = desired;
            velocity = Vector3.zero;
            snapNext = false;
        }
        else pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (useBounds || hasOverride)
        {
            Vector2 min = hasOverride ? overrideMin : minBounds;
            Vector2 max = hasOverride ? overrideMax : maxBounds;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            // If the screen is bigger than the bounds, stay centered (otherwise the camera jumps back and forth every frame)
            pos.x = max.x - min.x <= halfW * 2f
                ? (min.x + max.x) * 0.5f
                : Mathf.Clamp(pos.x, min.x + halfW, max.x - halfW);
            pos.y = max.y - min.y <= halfH * 2f
                ? (min.y + max.y) * 0.5f
                : Mathf.Clamp(pos.y, min.y + halfH, max.y - halfH);
        }

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            pos += (Vector3)(Random.insideUnitCircle * shakeMagnitude);
        }

        transform.position = pos;
    }

    static float Zoom => Mathf.Clamp(PlayerBalance.Current.cameraZoom, 0.3f, 2f);

    // Rings that take you up to the next floor: a ring sitting in a hole in a ceiling
    // (stone close on its left and right, open space above it), or one that leads to the next scene.
    // Other rings (swing rings out in the open, rings inside small pockets) are left alone. Worked out once per ring.
    readonly System.Collections.Generic.Dictionary<HookPoint, bool> upRings = new System.Collections.Generic.Dictionary<HookPoint, bool>();

    bool TakesYouUp(HookPoint r)
    {
        if (upRings.TryGetValue(r, out bool known)) return known;
        bool up = !string.IsNullOrEmpty(r.NextScene);
        if (!up)
        {
            int ground = LayerMask.GetMask("Ground");
            Vector2 p = r.transform.position;
            bool wallLeft = Physics2D.Raycast(p, Vector2.left, 3f, ground);
            bool wallRight = Physics2D.Raycast(p, Vector2.right, 3f, ground);
            bool openAbove = !Physics2D.Raycast(p, Vector2.up, 4f, ground);
            up = wallLeft && wallRight && openAbove;
        }
        upRings[r] = up;
        return up;
    }

    // The closest ring that takes you up, above you (or level with you) and near enough to use:
    // move the camera (and zoom out up to the scene's own size if needed) so both you and the ring are on screen.
    void FrameNearbyRing(ref Vector3 desired, ref float size)
    {
        float maxSize = Mathf.Max(size, normalSize / Zoom); // never further out than the scene's own camera size
        float reachX = maxSize * cam.aspect * 1.3f;
        float reachY = maxSize * 2f;
        Vector2 me = target.position;

        HookPoint ring = null;
        float best = float.MaxValue;
        foreach (HookPoint r in HookPoint.All)
        {
            if (!r || !TakesYouUp(r)) continue;
            Vector2 d = (Vector2)r.transform.position - me;
            if (d.y < -1f || d.y > reachY || Mathf.Abs(d.x) > reachX) continue;
            float score = d.sqrMagnitude;
            if (score < best) { best = score; ring = r; }
        }
        if (!ring) return;
        Vector2 rp = ring.transform.position;

        // Tall enough to show your feet and the ring together?
        float feet = me.y - 1.2f;
        float neededHalf = (rp.y + ringMargin - (feet - ringMargin)) * 0.5f;
        if (neededHalf > size) size = Mathf.Min(neededHalf, maxSize);
        float halfH = size, halfW = size * cam.aspect;

        // Vertically: high enough to show the ring, low enough to keep your feet on screen
        float lowest = rp.y + ringMargin - halfH;      // camera centre at least this high to see the ring
        float highest = feet - ringMargin + halfH;     // and at most this high to still see you
        if (desired.y < lowest) desired.y = Mathf.Min(lowest, highest);

        // Sideways: both you and the ring inside the view
        float lo = Mathf.Max(rp.x, me.x) - halfW + ringMargin;
        float hi = Mathf.Min(rp.x, me.x) + halfW - ringMargin;
        if (lo <= hi) desired.x = Mathf.Clamp(desired.x, lo, hi);
    }

    // Highest zone the player has climbed into (a zone starts 1 unit above its Bottom, so standing on its floor counts)
    int ZoneAt(float y)
    {
        int found = 0;
        for (int i = 0; i < zones.Length; i++)
            if (y >= zones[i].bottom + 1f) found = i;
        return found;
    }

    // Show only this area (for example a secret room) until ClearBoundsOverride is called
    public void SetBoundsOverride(Vector2 min, Vector2 max)
    {
        hasOverride = true;
        overrideMin = min;
        overrideMax = max;
        snapNext = true;
    }

    // Back to the normal camera
    public void ClearBoundsOverride()
    {
        hasOverride = false;
        snapNext = true;
    }

    public void Shake(float duration, float magnitude)
    {
        shakeTime = Mathf.Max(shakeTime, duration);
        shakeMagnitude = magnitude;
    }
}
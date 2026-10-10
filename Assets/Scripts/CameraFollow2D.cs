using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
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

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
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
            desired.y = firstFloorCenterY + floor * floorHeight;
        }

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
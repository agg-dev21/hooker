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
    [SerializeField] bool lockToFloors;            // ON: camera sirf us floor ko dikhata hai jis pe player hai
    [SerializeField] float floorHeight = 10f;       // ek floor kitna uncha (ek screen)
    [SerializeField] float firstFloorCenterY = 0f;  // sabse neeche wale floor ka center

    Camera cam;
    PlayerController2D player;
    Vector3 velocity;
    float currentLookAhead;
    float lookAheadVel;
    float trackedY;
    float shakeTime;
    float shakeMagnitude;

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

        // Floor lock: player jis floor pe hai uske center pe camera, upar neeche ke floors nahi dikhenge
        if (lockToFloors && floorHeight > 0f)
        {
            float bottom = firstFloorCenterY - floorHeight * 0.5f;
            int floor = Mathf.FloorToInt((target.position.y - bottom) / floorHeight);
            desired.y = firstFloorCenterY + floor * floorHeight;
        }

        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (useBounds)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            // Screen bounds se bada ho toh beech mein raho, warna camera har frame idhar udhar kudta hai
            pos.x = maxBounds.x - minBounds.x <= halfW * 2f
                ? (minBounds.x + maxBounds.x) * 0.5f
                : Mathf.Clamp(pos.x, minBounds.x + halfW, maxBounds.x - halfW);
            pos.y = maxBounds.y - minBounds.y <= halfH * 2f
                ? (minBounds.y + maxBounds.y) * 0.5f
                : Mathf.Clamp(pos.y, minBounds.y + halfH, maxBounds.y - halfH);
        }

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            pos += (Vector3)(Random.insideUnitCircle * shakeMagnitude);
        }

        transform.position = pos;
    }

    public void Shake(float duration, float magnitude)
    {
        shakeTime = Mathf.Max(shakeTime, duration);
        shakeMagnitude = magnitude;
    }
}
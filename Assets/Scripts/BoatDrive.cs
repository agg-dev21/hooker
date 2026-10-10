using UnityEngine;

// A boat the player steers. The player rides on it (their own movement is switched off).
// The move keys (A / D), the arrow keys, or the controller's left stick / D-pad move the boat left and right.
// When the boat has sailed fully off the right edge of the screen, the screen fades to black and Next Scene loads.
// Needs a SpriteRenderer (used to know how wide the boat is).
public class BoatDrive : MonoBehaviour
{
    [SerializeField] PlayerController2D rider;     // empty = finds the player in the scene
    [SerializeField] Vector2 riderOffset = new Vector2(0f, 1.5f); // where the player stands on the boat
    [SerializeField] float maxSpeed = 7f;
    [SerializeField] float acceleration = 10f;
    [SerializeField] float deceleration = 8f;
    [SerializeField] float leftLimit = -12f;       // the boat cannot go further left than this
    [SerializeField] string nextScene = "Level_2";
    [SerializeField] float fadeOutTime = 1f;

    float speed;
    bool leaving;
    Camera cam;
    SpriteRenderer sr;

    void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        if (!rider) rider = FindFirstObjectByType<PlayerController2D>();
        if (!rider) return;

        // The player stops moving on their own and rides the boat
        rider.enabled = false;
        if (rider.TryGetComponent(out PlayerCombat combat)) combat.enabled = false;
        if (rider.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
        rider.transform.SetParent(transform);
        rider.transform.position = transform.position + (Vector3)riderOffset;
    }

    void Update()
    {
        float input = leaving ? 0f : ReadInput();
        float target = input * maxSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, target, rate * Time.deltaTime);

        Vector3 pos = transform.position + Vector3.right * speed * Time.deltaTime;
        if (pos.x < leftLimit) { pos.x = leftLimit; speed = 0f; }
        transform.position = pos;

        // Fully off the right edge of the screen: fade to the next scene
        if (!leaving && cam)
        {
            float halfWidth = sr ? sr.bounds.extents.x : 1f;
            float screenRight = cam.transform.position.x + cam.orthographicSize * cam.aspect;
            if (transform.position.x - halfWidth > screenRight)
            {
                leaving = true;
                ScreenFade.FadeToScene(nextScene, fadeOutTime);
            }
        }
    }

    static float ReadInput()
    {
        // Move keys (A / D by default), the left/right aim keys (arrows) or the controller
        float x = GameInput.MoveX;
        if (x == 0f)
        {
            if (GameInput.KeyHeld(GameAction.AimLeft)) x -= 1f;
            if (GameInput.KeyHeld(GameAction.AimRight)) x += 1f;
        }
        return Mathf.Clamp(x, -1f, 1f);
    }
}
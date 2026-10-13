using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A thin platform you can jump up through from below (needs a PlatformEffector2D with One Way on).
// Stand on it and hold Down (S, Down arrow / Aim Down key, or controller stick / D-pad down) to drop through it.
// You don't have to add it by hand: when a scene loads, every one-way PlatformEffector2D gets this automatically.
[RequireComponent(typeof(Collider2D))]
public class OneWayPlatform : MonoBehaviour
{
    [SerializeField] float holdTime = 0.2f;  // hold Down this long to drop (so a quick tap to aim down doesn't drop you)
    [SerializeField] float dropTime = 0.35f; // the platform ignores the player this long, enough to fall through

    Collider2D col;
    PlatformEffector2D effector;
    PlayerController2D player;
    Rigidbody2D playerRb;
    Collider2D[] playerColliders;
    float held;
    float dropTimer;

    // Add this to every one-way platform in every scene, so it works even in levels built before this script existed
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        AttachToAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToAll();

    static void AttachToAll()
    {
        int added = 0;
        foreach (PlatformEffector2D e in FindObjectsByType<PlatformEffector2D>(FindObjectsInactive.Exclude))
            if (e.useOneWay && !e.GetComponent<OneWayPlatform>()) { e.gameObject.AddComponent<OneWayPlatform>(); added++; }
        if (added > 0) Debug.Log("OneWayPlatform: added drop-through to " + added + " platform(s)");
    }

    void Start()
    {
        col = GetComponent<Collider2D>();
        effector = GetComponent<PlatformEffector2D>();
        player = FindAnyObjectByType<PlayerController2D>();
        if (!player) { Debug.LogWarning("OneWayPlatform: no player found, dropping won't work on " + name, this); return; }
        playerRb = player.GetComponent<Rigidbody2D>();
        playerColliders = player.GetComponentsInChildren<Collider2D>();
    }

    void Update()
    {
        if (!player || Time.timeScale == 0f) return;

        // Dropping: let the player fall through, then become solid again
        if (dropTimer > 0f)
        {
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0f && StillInside()) dropTimer = 0.05f; // still passing through: wait a little longer
            if (dropTimer <= 0f)
            {
                SetIgnore(false);
                if (effector) effector.rotationalOffset = 0f;
            }
            return;
        }

        if (StandingOnMe() && DownHeld())
        {
            held += Time.deltaTime;
            if (held >= holdTime)
            {
                held = 0f;
                dropTimer = dropTime;
                SetIgnore(true);
                if (effector) effector.rotationalOffset = 180f; // flip the one-way side too, so nothing holds the player up
                Debug.Log("OneWayPlatform: dropping through " + name, this);
                if (playerRb) playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, -3f); // start falling right away
            }
        }
        else held = 0f;
    }

    // Is the player's feet resting on top of this platform?
    bool StandingOnMe()
    {
        Bounds top = col.bounds;
        foreach (Collider2D c in playerColliders)
        {
            if (!c || c.isTrigger) continue;
            Bounds feet = c.bounds;
            bool onTop = feet.min.y >= top.max.y - 0.35f && feet.min.y <= top.max.y + 0.35f;
            bool overlapsX = feet.max.x > top.min.x && feet.min.x < top.max.x;
            if (onTop && overlapsX) return true;
        }
        return false;
    }

    bool StillInside()
    {
        foreach (Collider2D c in playerColliders)
            if (c && !c.isTrigger && c.Distance(col).isOverlapped) return true;
        return false;
    }

    static bool DownHeld()
    {
        if (GameInput.KeyHeld(GameAction.AimDown)) return true;              // Down arrow (or whatever Aim Down is set to)
        if (Keyboard.current != null && Keyboard.current.sKey.isPressed) return true; // S, next to A / D
        Gamepad gp = Gamepad.current;
        return gp != null && (gp.leftStick.y.ReadValue() < -0.6f || gp.dpad.down.isPressed);
    }

    void SetIgnore(bool ignore)
    {
        foreach (Collider2D c in playerColliders)
            if (c && !c.isTrigger) Physics2D.IgnoreCollision(c, col, ignore);
    }
}
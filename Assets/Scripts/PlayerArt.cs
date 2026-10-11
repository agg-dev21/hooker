using UnityEngine;
using UnityEngine.SceneManagement;

// Shows the main character's drawings instead of the plain box, and picks the animation from what he is doing:
// standing, running, jumping / falling / landing, dashing, slashing (punch) and throwing the hook.
// Colour effects on the player (poison green, hit flash, blinking) show on the drawing too.
// Adds itself to the player in every scene once Tools > Art > Raphael (Player) has been run. His hit box is unchanged.
public class PlayerArt : MonoBehaviour
{
    PlayerArtSet A => PlayerArtSet.Current;

    PlayerController2D controller;
    PlayerCombat combat;
    Rigidbody2D rb;
    SpriteRenderer box;
    Color boxColor;
    SpriteRenderer art;

    float runTime, dashTime, landTimer;
    bool wasGrounded = true, wasDashing;

    // ---------------- Adds itself to the player in every scene ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToPlayer();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToPlayer();

    static void AddToPlayer()
    {
        PlayerArtSet set = PlayerArtSet.Current;
        if (!set || set.run == null || set.run.Length == 0) return; // art not set up yet: keep the box
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc && !pc.GetComponent<PlayerArt>()) pc.gameObject.AddComponent<PlayerArt>();
    }

    // ---------------- Setup ----------------

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        combat = GetComponent<PlayerCombat>();
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<SpriteRenderer>();
        if (box) boxColor = box.color;

        var go = new GameObject("Art");
        go.transform.SetParent(transform, false);
        art = go.AddComponent<SpriteRenderer>();
        art.sprite = A.run[0];
        if (box)
        {
            art.sortingLayerID = box.sortingLayerID;
            art.sortingOrder = box.sortingOrder;
            box.enabled = false;
        }
        PlaceAtFeet();
    }

    // The drawing stands on the bottom of his hit box, at normal size whatever the player's scale is
    void PlaceAtFeet()
    {
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Abs(s.x) < 0.0001f ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Abs(s.y) < 0.0001f ? 1f : Mathf.Abs(s.y);
        float k = Mathf.Max(A.drawScale, 0.1f); // bigger drawing, same hit box
        art.transform.localScale = new Vector3(k / sx, k / sy, 1f);
        float feet = transform.position.y - 1f;
        if (TryGetComponent(out Collider2D col)) feet = col.bounds.min.y;
        art.transform.localPosition = new Vector3(0f, (feet - transform.position.y + A.heightOffset) / sy, 0f);
    }

    // ---------------- Every frame ----------------

    void LateUpdate()
    {
        if (box && box.enabled) box.enabled = false; // something switched the box back on: keep showing the drawing

        // Colour effects (poison, burn, hit flash) are put on the box: copy them onto the drawing
        if (box) art.color = box.color == boxColor ? Color.white : box.color;

        bool grounded = controller && controller.IsGrounded;
        bool dashing = controller && controller.IsDashing;
        float vx = rb ? rb.linearVelocity.x : 0f;
        float vy = rb ? rb.linearVelocity.y : 0f;
        float facing = controller ? controller.FacingDirection : 1f;

        if (grounded && !wasGrounded) landTimer = A.landTime;
        wasGrounded = grounded;
        if (landTimer > 0f) landTimer -= Time.deltaTime;
        if (dashing && !wasDashing) dashTime = 0f;
        wasDashing = dashing;

        float sincePunch = combat ? Time.time - combat.LastPunchTime : 99f;
        float sinceHook = combat ? Time.time - combat.LastHookTime : 99f;
        int attackFrames = Count(A.attack) - 1;
        int hookFrames = Count(A.hook) - 1;

        Sprite frame;
        if (controller && !controller.enabled)
        {
            frame = A.run[0]; // riding a boat or being carried: just stand
        }
        else if (attackFrames > 0 && sincePunch < attackFrames / A.attackFps)
        {
            frame = A.attack[1 + Mathf.Min(attackFrames - 1, Mathf.FloorToInt(sincePunch * A.attackFps))];
            facing = Side(combat.LastAttackDir.x, facing);
        }
        else if (hookFrames > 0 && (sinceHook < hookFrames / A.hookFps || (combat && combat.HookOut)))
        {
            frame = A.hook[1 + Mathf.Min(hookFrames - 1, Mathf.FloorToInt(sinceHook * A.hookFps))]; // holds the last frame while the hook is out
            facing = Side(combat.LastAttackDir.x, facing);
        }
        else if (dashing && Count(A.dash) > 2)
        {
            dashTime += Time.deltaTime;
            int moving = Count(A.dash) - 2; // skip the standing frames at both ends
            frame = A.dash[1 + Mathf.Min(moving - 1, Mathf.FloorToInt(dashTime * A.dashFps))];
        }
        else if (!grounded && Count(A.jump) > 2)
        {
            frame = vy > 0.5f ? A.jump[1] : A.jump[2];
        }
        else if (landTimer > 0f && Count(A.jump) > 3)
        {
            frame = A.jump[3];
        }
        else if (Mathf.Abs(vx) > 0.5f && Count(A.run) > 1)
        {
            runTime += Time.deltaTime;
            int moving = Count(A.run) - 1;
            frame = A.run[1 + Mathf.FloorToInt(runTime * A.runFps) % moving];
        }
        else
        {
            runTime = 0f;
            frame = A.run[0]; // standing
        }

        art.sprite = frame;
        // The player turns around by flipping his whole body, which already turns the drawing.
        // Flip it only when an attack points the other way, or if the drawings face left.
        float bodyFacing = controller ? controller.FacingDirection : 1f;
        art.flipX = (Mathf.Sign(facing) != Mathf.Sign(bodyFacing)) ^ !A.drawnFacingRight;
    }

    static int Count(Sprite[] frames) => frames == null ? 0 : frames.Length;

    // Faces the attack direction when it points left or right, otherwise keeps the current facing
    static float Side(float x, float fallback) => Mathf.Abs(x) > 0.1f ? Mathf.Sign(x) : fallback;
}

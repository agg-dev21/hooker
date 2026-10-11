using UnityEngine;

// A platform that slides back and forth between its start and start + Travel, pausing at each end.
// Carries the player when they stand on it. Needs a solid BoxCollider2D.
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] Vector2 travel = new Vector2(5f, 0f); // how far it moves from where it starts
    [SerializeField] float speed = 3f;
    [SerializeField] float pause = 0.6f;                    // wait at each end
    [SerializeField] float startDelay = 0f;

    Rigidbody2D rb;
    BoxCollider2D col;
    Vector2 a, b;
    bool toB = true;
    float wait;
    Rigidbody2D playerRb;
    Collider2D playerCol;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        col = GetComponent<BoxCollider2D>();
        a = rb.position;
        b = a + travel;
        wait = startDelay;
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (!pc) return;
        playerRb = pc.GetComponent<Rigidbody2D>();
        playerCol = pc.GetComponent<Collider2D>();
    }

    void FixedUpdate()
    {
        if (wait > 0f) { wait -= Time.fixedDeltaTime; return; }

        Vector2 target = toB ? b : a;
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        Vector2 delta = next - rb.position;
        bool carry = PlayerOnTop();
        rb.MovePosition(next);
        if (carry && playerRb) playerRb.position += delta; // take the player along

        if ((next - target).sqrMagnitude < 0.0001f)
        {
            toB = !toB;
            wait = pause;
        }
    }

    bool PlayerOnTop()
    {
        if (!playerCol) return false;
        Bounds p = playerCol.bounds, me = col.bounds;
        return p.min.y >= me.max.y - 0.2f && p.min.y <= me.max.y + 0.25f && p.max.x > me.min.x && p.min.x < me.max.x;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? (Vector3)a : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, start + (Vector3)travel);
        Gizmos.DrawWireCube(start + (Vector3)travel, transform.lossyScale);
    }
}
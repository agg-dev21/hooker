using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChase : MonoBehaviour
{
    [SerializeField] float speed = 2.5f;
    [SerializeField] float detectRange = 10f;  // start chasing when the player is this close
    [SerializeField] float stopDistance = 3f;  // stop this far from the player (and shoot)
    [SerializeField] LayerMask groundLayer;

    Rigidbody2D rb;
    Collider2D col;
    Transform player;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc) player = pc.transform;
    }

    void FixedUpdate()
    {
        float moveDir = 0f;

        if (player)
        {
            float dx = player.position.x - transform.position.x;
            float distance = Vector2.Distance(transform.position, player.position);

            // Player is in range but still far: move toward them
            if (distance < detectRange && Mathf.Abs(dx) > stopDistance)
                moveDir = Mathf.Sign(dx);
        }

        if (moveDir != 0f)
        {
            Face(moveDir);
            if (!CanMove(moveDir)) moveDir = 0f; // edge or wall ahead: stop
        }

        rb.linearVelocity = new Vector2(moveDir * speed, rb.linearVelocity.y);
    }

    // Only move if there is ground ahead and no wall
    bool CanMove(float dir)
    {
        Bounds b = col.bounds;
        Vector2 front = new Vector2(dir > 0 ? b.max.x : b.min.x, b.center.y);
        bool wallAhead = Physics2D.Raycast(front, Vector2.right * dir, 0.2f, groundLayer);

        Vector2 foot = new Vector2(front.x + dir * 0.05f, b.min.y);
        bool groundAhead = Physics2D.Raycast(foot, Vector2.down, 0.3f, groundLayer);

        return !wallAhead && groundAhead;
    }

    void Face(float dir)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * Mathf.Sign(dir);
        transform.localScale = s;
    }

    // In the Scene view: yellow circle = detect range, blue = stop distance
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
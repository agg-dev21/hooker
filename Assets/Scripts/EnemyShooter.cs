using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float range = 8f;        // only shoot when the player is this close
    [SerializeField] float fireRate = 1.5f;   // seconds between shots
    [SerializeField] bool aimAtPlayer = true; // false = shoot straight left/right only

    Transform player;
    float timer;

    void Start()
    {
        PlayerController2D pc = FindFirstObjectByType<PlayerController2D>();
        if (pc) player = pc.transform;
        timer = fireRate;
    }

    void Update()
    {
        if (!player) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > range) return; // player is too far, do nothing

        // Turn to face the player
        float dirX = Mathf.Sign(player.position.x - transform.position.x);
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dirX;
        transform.localScale = s;

        // Timer done: shoot
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Shoot(dirX);
            timer = fireRate;
        }
    }

    void Shoot(float dirX)
    {
        Vector3 spawn = firePoint ? firePoint.position : transform.position;
        Vector2 dir = aimAtPlayer ? (Vector2)(player.position - spawn) : new Vector2(dirX, 0f);

        Projectile p = Instantiate(projectilePrefab, spawn, Quaternion.identity);
        p.Launch(dir);
    }

    // Shows the range as a red circle in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
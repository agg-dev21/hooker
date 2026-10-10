using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float range = 8f;        // player itna paas ho tabhi shoot kare
    [SerializeField] float fireRate = 1.5f;   // har goli ke beech itne second
    [SerializeField] bool aimAtPlayer = true; // false = sirf seedha left/right

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
        if (distance > range) return; // player door hai, kuch mat karo

        // Player ki taraf mudo
        float dirX = Mathf.Sign(player.position.x - transform.position.x);
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dirX;
        transform.localScale = s;

        // Timer khatam, toh goli chalao
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

    // Scene mein range ka red circle dikhata hai
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    Vector3 respawnPoint;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        respawnPoint = transform.position; // start position = pehla respawn point
    }

    public void SetRespawnPoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector2.zero; // girne ki speed band karo
        transform.position = respawnPoint;
    }
}
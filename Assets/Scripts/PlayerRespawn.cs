using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    Vector3 respawnPoint;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        respawnPoint = transform.position; // starting position is the first respawn point
    }

    public void SetRespawnPoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector2.zero; // stop any falling speed
        transform.position = respawnPoint;
    }
}
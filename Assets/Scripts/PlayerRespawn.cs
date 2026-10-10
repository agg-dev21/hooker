using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    // Fired every time the player respawns (died from damage OR fell into a kill zone).
    // Bosses listen to this to reset their fight.
    public static event System.Action Respawned;

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
        Respawned?.Invoke();
    }
}
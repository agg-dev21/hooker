using UnityEngine;

public class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        // Respawn anything that enters and has PlayerRespawn
        if (other.TryGetComponent(out PlayerRespawn player))
        {
            player.Respawn();
        }
    }
}
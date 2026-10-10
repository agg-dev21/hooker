using UnityEngine;

public class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        // Jo andar aaya, agar uspe PlayerRespawn hai, toh respawn karo
        if (other.TryGetComponent(out PlayerRespawn player))
        {
            player.Respawn();
        }
    }
}
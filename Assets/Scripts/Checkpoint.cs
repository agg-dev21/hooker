using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] Color activeColor = Color.green;
    bool activated;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return; // ek hi baar chale

        if (other.TryGetComponent(out PlayerRespawn player))
        {
            player.SetRespawnPoint(transform.position);
            activated = true;

            // Color badlo taaki pata chale active ho gaya
            if (TryGetComponent(out SpriteRenderer sr))
                sr.color = activeColor;
        }
    }
}

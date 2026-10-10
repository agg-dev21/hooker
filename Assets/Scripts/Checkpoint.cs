using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] Color activeColor = Color.green;
    bool activated;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return; // only activate once

        if (other.TryGetComponent(out PlayerRespawn player))
        {
            player.SetRespawnPoint(transform.position);
            activated = true;

            // Change color to show it is active
            if (TryGetComponent(out SpriteRenderer sr))
                sr.color = activeColor;
        }
    }
}

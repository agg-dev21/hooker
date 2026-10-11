using UnityEngine;

public class LevelGoal : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        // The player reached the goal
        if (other.TryGetComponent(out PlayerController2D _) && GameManager.Instance != null)
            GameManager.Instance.Win();
    }
}

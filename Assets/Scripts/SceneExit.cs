using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneExit : MonoBehaviour
{
    [SerializeField] string nextScene; // agle scene ka exact naam

    void OnTriggerEnter2D(Collider2D other)
    {
        // Player screen ke end tak pahuncha: agla scene load karo
        if (other.TryGetComponent(out PlayerController2D _) && !string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}
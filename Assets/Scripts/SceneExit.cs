using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneExit : MonoBehaviour
{
    [SerializeField] string nextScene; // exact name of the next scene

    void OnTriggerEnter2D(Collider2D other)
    {
        // The player reached the edge of the screen: load the next scene
        if (other.TryGetComponent(out PlayerController2D _) && !string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(RunOrder.Resolve(nextScene));
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene(1); // game wala scene
    }

    public void Quit()
    {
        Application.Quit();
    }
}

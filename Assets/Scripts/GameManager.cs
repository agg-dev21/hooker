using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject pausePanel;

    bool paused;
    bool won;

    public bool IsPausedOrWon => paused || won;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (winPanel) winPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
    }

    void Update()
    {
        // Esc or controller Start (Menu) to pause
        if (GameInput.PausePressed)
            TogglePause();
    }

    public void Win()
    {
        won = true;
        if (winPanel) winPanel.SetActive(true);
        SelectFirstButton(winPanel);
        Time.timeScale = 0f; // freeze the game
    }

    public void TogglePause()
    {
        if (won) return; // no pausing after winning
        paused = !paused;
        if (pausePanel) pausePanel.SetActive(paused);
        if (paused) SelectFirstButton(pausePanel);
        Time.timeScale = paused ? 0f : 1f;
    }

    // Highlight the first button in a menu so a controller can navigate it (D-pad / stick, A to press)
    static void SelectFirstButton(GameObject panel)
    {
        if (!panel || EventSystem.current == null) return;
        Button first = panel.GetComponentInChildren<Button>();
        if (first) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    // These functions are called by UI buttons
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
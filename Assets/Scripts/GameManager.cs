using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject pausePanel; // leave empty to use the built-in pause menu (with Controls)

    bool paused;
    bool won;
    PauseMenu menu;

    public bool IsPausedOrWon => paused || won;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (winPanel) winPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        else menu = PauseMenu.Create(this); // no custom panel: build the pause menu in code
    }

    void Update()
    {
        if (menu && menu.IsListening) return; // waiting for a new key in the Controls page

        // Pause key (Esc / controller Menu by default). With the inventory or a note open, Esc closes that instead.
        if (GameInput.PausePressed && !(PlayerItems.ScreenOpen && !paused))
        {
            if (menu && menu.InControls) menu.ShowMain(); // from Controls, go back one page first
            else TogglePause();
        }
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

        if (pausePanel)
        {
            pausePanel.SetActive(paused);
            if (paused) SelectFirstButton(pausePanel);
        }
        if (menu)
        {
            if (paused) menu.Open();
            else menu.Close();
        }

        Time.timeScale = paused ? 0f : 1f;
    }

    // Resume button. Waits one frame so the button press (e.g. A or Space) doesn't also make the player jump.
    public void Resume()
    {
        if (paused) StartCoroutine(ResumeNextFrame());
    }

    IEnumerator ResumeNextFrame()
    {
        yield return null;
        if (paused) TogglePause();
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stop Play mode in the editor
#else
        Application.Quit();
#endif
    }
}
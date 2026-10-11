using UnityEngine;

// The door to the boss room. Stand at it and press Interact (E / controller Y by default):
// the screen fades to black and the boss scene loads.
// Needs a trigger Collider2D.
[RequireComponent(typeof(Collider2D))]
public class BossDoor : MonoBehaviour
{
    [SerializeField] string bossScene = "Level_2_Boss"; // exact scene name (must be in the Scene List)
    [SerializeField] GameObject prompt;                  // "Press E" pop-up, shown while the player is at the door

    bool playerNear;
    bool used;

    void Update()
    {
        if (used || !playerNear || Time.timeScale == 0f) return;
        if (!GameInput.InteractPressed) return;
        used = true;
        if (prompt) prompt.SetActive(false);
        ScreenFade.FadeToScene(bossScene);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = true;
        if (prompt && !used) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = false;
        if (prompt) prompt.SetActive(false);
    }
}

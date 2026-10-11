using UnityEngine;

// For fights with more than one boss: when ALL the listed bosses are dead, it hides "Remove When All Dead"
// (for example the exit gate) and shows "Show When All Dead" (for example the exit door).
public class BossGroupGate : MonoBehaviour
{
    [SerializeField] Health[] bosses;
    [SerializeField] GameObject[] removeWhenAllDead;
    [SerializeField] GameObject[] showWhenAllDead;

    bool done;

    void Update()
    {
        if (done) return;
        foreach (Health h in bosses)
            if (h && h.CurrentHealth > 0) return; // someone is still fighting

        done = true;
        foreach (GameObject go in removeWhenAllDead) if (go) go.SetActive(false);
        foreach (GameObject go in showWhenAllDead) if (go) go.SetActive(true);
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.5f, 0.4f);
    }
}
using System.Collections;
using UnityEngine;

// When the boss is beaten (and any dialogue has finished), wait a moment, then fade to another scene.
// Used after the final boss: KK's last words, then the cliff with Mom's grave (Ending_Grave).
public class AfterBossScene : MonoBehaviour
{
    [SerializeField] Health boss;
    [SerializeField] string sceneName = "Ending_Grave";
    [SerializeField] float delay = 2f;

    bool started, going;

    public void Set(Health h, string scene) { boss = h; sceneName = scene; }

    void Update()
    {
        if (going) return;
        if (boss) { started = true; if (boss.CurrentHealth > 0) return; }
        else if (!started) return; // never saw the boss
        going = true;
        StartCoroutine(Go());
    }

    IEnumerator Go()
    {
        yield return new WaitForSeconds(0.8f); // let the defeat dialogue open first
        while (DialogueManager.Busy) yield return null;
        yield return new WaitForSeconds(delay);
        ScreenFade.FadeToScene(sceneName, 1.5f, 1.5f);
    }
}

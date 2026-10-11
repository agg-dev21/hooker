using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// When the boss is beaten (and any dialogue has finished), wait a moment, then fade to another scene.
// Used after the final boss: KK's last words, then the cliff with Mom's grave (Ending_Grave).
//
// It also installs itself: whenever Level_4_Boss loads without one (for example if
// Tools > Story > Add Ending Hook And Ariel Talks was never run), it adds KK's last words
// and the fade to Ending_Grave by itself. Nothing needs to be set up in the scene.
public class AfterBossScene : MonoBehaviour
{
    const string FinalBossScene = "Level_4_Boss";
    const string EndingScene = "Ending_Grave";

    [SerializeField] Health boss;
    [SerializeField] string sceneName = EndingScene;
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
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
            Debug.LogError("AfterBossScene: '" + sceneName + "' is not in the Scene List (File > Build Profiles). " +
                           "Build it with Tools > Story > Build Ending (Mother's Grave).");
        ScreenFade.FadeToScene(sceneName, 1.5f, 1.5f);
    }

    // ---------------- Automatic setup for the final boss ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookFinalBoss(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookFinalBoss(scene);

    static void HookFinalBoss(Scene scene)
    {
        if (scene.name != FinalBossScene) return;
        if (FindAnyObjectByType<AfterBossScene>(FindObjectsInactive.Include)) return; // already set up in the scene
        FatherBoss kk = FindAnyObjectByType<FatherBoss>(FindObjectsInactive.Include);
        if (!kk || !kk.TryGetComponent(out Health h)) { Debug.LogWarning("AfterBossScene: no KK (FatherBoss) in " + scene.name); return; }

        var go = new GameObject("EndingHook (auto)");
        var d = go.AddComponent<DefeatDialogue>();
        d.SetBoss(h);
        d.lines = KkLastWords();
        go.AddComponent<AfterBossScene>().Set(h, EndingScene);
    }

    static List<DialogueLine> KkLastWords()
    {
        const string R = "Raphael", K = "KK";
        string[] pairs =
        {
            K, "Heh... the one they call the Flaw. And still my greatest deal.",
            R, "Why didn't you ever look for me?",
            K, "The milk was running low.",
            R, "...Guess you're no better than the rest.",
            K, "No. I'm not.",
            K, "Your mother. You asked where she is.",
            K, "Past this hall there's a path up the cliff. She asked to be buried where she could see the sea.",
            K, "She left something for you. She made me promise to keep it there until you came.",
            R, "...She's gone?",
            K, "A long time now. Go on, kid. She waited long enough.",
        };
        var lines = new List<DialogueLine>();
        for (int i = 0; i + 1 < pairs.Length; i += 2) lines.Add(new DialogueLine { speaker = pairs[i], text = pairs[i + 1] });
        return lines;
    }
}
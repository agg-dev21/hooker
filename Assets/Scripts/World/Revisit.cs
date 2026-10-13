using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Going back to a level whose boss you already beat (picked on the boat, see SailMenu):
//   - The beaten boss stays beaten: Hardy's deck is empty (it asks to set sail), Oscar's room is empty with its exit
//     open, and in Level 3 both Madame O and Oscar are gone so the gate is open. Their talk doesn't play again.
//   - At the start of the level there is a spot that takes you back to the boat (press Interact there).
// Adds itself to every scene, nothing to set up.
public class Revisit : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "Level_1_Deck":
                if (!RunOrder.HardyDone) return;
                foreach (HardyBoss b in FindObjectsByType<HardyBoss>(FindObjectsInactive.Include)) Destroy(b.gameObject);
                RemoveStoryTalk();
                LevelEndPrompt.Show(RunOrder.SailScene, 1.5f, "Hardy's deck is quiet now.\nSet sail?");
                break;

            case "Level_2_Boss":
                if (!RunOrder.OscarDone) return;
                foreach (PoisonBoss b in FindObjectsByType<PoisonBoss>(FindObjectsInactive.Include))
                {
                    b.SendMessage("OnDied", SendMessageOptions.DontRequireReceiver); // opens his exit like when he ran away
                    Destroy(b.gameObject);
                }
                RemoveStoryTalk();
                break;

            case "Level_3_Boss":
                if (!RunOrder.MadameDone) return;
                foreach (OctopusBoss b in FindObjectsByType<OctopusBoss>(FindObjectsInactive.Include)) Destroy(b.gameObject);
                foreach (PoisonBoss b in FindObjectsByType<PoisonBoss>(FindObjectsInactive.Include)) Destroy(b.gameObject);
                RemoveStoryTalk(); // the gate opens by itself once both are gone
                break;

            case "Level_1":
            case "Level_2":
            case "Level_3":
                if (RunOrder.LevelDone(scene.name)) BackToBoatSpot();
                break;
        }
    }

    // The boss intro and "beaten" lines only belong to the first visit
    static void RemoveStoryTalk()
    {
        foreach (SceneDialogue d in FindObjectsByType<SceneDialogue>(FindObjectsInactive.Include)) Destroy(d.gameObject);
        foreach (DefeatDialogue d in FindObjectsByType<DefeatDialogue>(FindObjectsInactive.Include)) Destroy(d.gameObject);
    }

    // A spot where the player starts: stand on it and press Interact to sail back
    static void BackToBoatSpot()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (!pc) return;
        var go = new GameObject("BackToTheBoat");
        go.transform.position = pc.transform.position;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.5f, 3f);
        go.AddComponent<Revisit>();
    }

    // ---------------- The spot ----------------

    bool playerNear, used;
    TMP_Text label;

    void Start()
    {
        var t = new GameObject("Label");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        label = t.AddComponent<TextMeshPro>();
        label.fontSize = 3.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        label.color = new Color(1f, 0.85f, 0.35f);
        label.GetComponent<MeshRenderer>().sortingOrder = 30;
        label.text = "";
    }

    void Update()
    {
        if (!label) return;
        label.text = playerNear && !used ? "Press " + GameInput.KeyName(GameInput.GetKey(GameAction.Interact)) + ": back to the boat" : "";
        if (used || !playerNear || Time.timeScale == 0f) return;
        if (GameInput.InteractPressed)
        {
            used = true;
            ScreenFade.FadeToScene(RunOrder.SailScene);
        }
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.TryGetComponent(out PlayerController2D _)) playerNear = true; }
    void OnTriggerExit2D(Collider2D other) { if (other.TryGetComponent(out PlayerController2D _)) playerNear = false; }
}

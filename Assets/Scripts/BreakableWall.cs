using UnityEngine;

// A wall that breaks when its Health runs out (punches, hook, or a barrel explosion).
// When it breaks, everything in Reveal On Break is hidden (for example a dark cover over a secret room).
// Needs a solid Collider2D and a Health component.
[RequireComponent(typeof(Health))]
public class BreakableWall : MonoBehaviour
{
    [SerializeField] GameObject[] revealOnBreak;

    void Awake()
    {
        GetComponent<Health>().onDied.AddListener(Break);
    }

    void Break()
    {
        foreach (GameObject go in revealOnBreak)
            if (go) go.SetActive(false);
    }
}

using UnityEngine;

// Hook ring. When the hook (right click) hits it, the player is pulled to the ring.
// Needs a Collider2D (Is Trigger ON).
public class HookPoint : MonoBehaviour
{
    [SerializeField] float launchSpeed = 10f; // how hard the player is launched upward after reaching the ring
    [SerializeField] string nextScene;        // if filled: on reaching the ring the screen fades to black and this scene loads

    public float LaunchSpeed => launchSpeed;
    public string NextScene => nextScene;
}

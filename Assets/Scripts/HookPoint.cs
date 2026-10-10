using UnityEngine;

// Hook ring. Right click pe hook isse lage toh player khud ring tak khinch jaata hai.
// Object pe ek Collider2D chahiye (Is Trigger ON).
public class HookPoint : MonoBehaviour
{
    [SerializeField] float launchSpeed = 10f; // ring tak pahunch ke player ko upar kitna uchhale
    [SerializeField] string nextScene;        // bhara hai toh: ring tak pahunch ke screen black, ye scene load

    public float LaunchSpeed => launchSpeed;
    public string NextScene => nextScene;
}

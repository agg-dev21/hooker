using System.Collections.Generic;
using UnityEngine;

// Hook ring. When the hook hits it, the player is pulled to the ring.
// Needs a Collider2D (Is Trigger ON).
public class HookPoint : MonoBehaviour
{
    [SerializeField] float launchSpeed = 10f; // upward launch speed after reaching the ring
    [SerializeField] string nextScene;        // if set: on reaching the ring, fade to black and load this scene

    public float LaunchSpeed => launchSpeed;
    public string NextScene => nextScene;

    // All rings in the scene (used by aim assist, so we never have to search the scene)
    public static readonly List<HookPoint> All = new List<HookPoint>();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);
}
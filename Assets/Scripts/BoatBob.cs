using UnityEngine;

// Rocks a boat on the waves: a gentle bob up and down and a small tilt. Whoever rides it rocks along.
// Works together with the scripts that move the boat sideways (BoatDrive, BoatExit). Look only.
public class BoatBob : MonoBehaviour
{
    [SerializeField] float bob = 0.12f;       // how far it moves up and down
    [SerializeField] float bobSpeed = 1.6f;
    [SerializeField] float tilt = 3f;         // degrees
    [SerializeField] float tiltSpeed = 1.2f;

    float baseY;
    float lastOffset;

    void Start() => baseY = transform.position.y;

    void LateUpdate()
    {
        // Take back last frame's bob, add this frame's (so the sideways movement from other scripts is kept)
        float offset = Mathf.Sin(Time.time * bobSpeed) * bob;
        Vector3 p = transform.position;
        p.y += offset - lastOffset;
        transform.position = p;
        lastOffset = offset;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * tiltSpeed + 0.7f) * tilt);
    }
}

using UnityEngine;

// A spiked poison ball hanging on a chain that swings left and right.
// This object is the pivot (where the chain is attached). The chain and ball are its children,
// so rotating this object swings them. The ball needs a trigger collider and a PoisonHazard.
public class PoisonPendulum : MonoBehaviour
{
    [SerializeField] float swingAngle = 50f;  // degrees to each side
    [SerializeField] float period = 2.6f;     // seconds for one full swing (there and back)
    [SerializeField] float phase = 0f;        // 0..1, offset so pendulums don't swing together

    void Update()
    {
        float angle = swingAngle * Mathf.Sin((Time.time / period + phase) * Mathf.PI * 2f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}

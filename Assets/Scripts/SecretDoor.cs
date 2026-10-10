using UnityEngine;
using UnityEngine.InputSystem;

// A door the player can use by standing at it and pressing the use key.
// It moves the player to Destination. Used for secret rooms that sit outside the main level:
// - Entrance door (Enters Room ON): the camera switches to show only the room (Room Min / Room Max).
// - Exit door (Enters Room OFF): the camera goes back to normal.
// Needs a SpriteRenderer and a Collider2D with Is Trigger ON.
public class SecretDoor : MonoBehaviour
{
    [SerializeField] Key useKey = Key.E;
    [SerializeField] Transform destination;
    [SerializeField] bool entersRoom = true;
    [SerializeField] Vector2 roomMin;   // bottom-left corner of the room (for the camera)
    [SerializeField] Vector2 roomMax;   // top-right corner of the room (for the camera)
    [SerializeField] GameObject prompt; // "Press E" pop-up, shown while the player is at the door

    Transform player;

    void Awake()
    {
        if (prompt) prompt.SetActive(false);
    }

    void Update()
    {
        if (!player || !destination || Keyboard.current == null) return;
        if (Keyboard.current[useKey].wasPressedThisFrame) Use();
    }

    void Use()
    {
        player.position = destination.position;
        if (player.TryGetComponent(out Rigidbody2D rb)) rb.linearVelocity = Vector2.zero;

        CameraFollow2D cam = CameraFollow2D.Instance;
        if (cam)
        {
            if (entersRoom) cam.SetBoundsOverride(roomMin, roomMax);
            else cam.ClearBoundsOverride();
        }
        player = null; // the player is no longer at this door
        if (prompt) prompt.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        player = other.transform;
        if (prompt) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        player = null;
        if (prompt) prompt.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        if (!entersRoom) return;
        Gizmos.color = Color.cyan;
        Vector3 center = (roomMin + roomMax) * 0.5f;
        Gizmos.DrawWireCube(center, roomMax - roomMin);
    }
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// A chest. Stand next to it and press the open key to open it.
// For now it gives nothing: hook up items later through the On Opened event in the Inspector.
// Needs a SpriteRenderer and a Collider2D with Is Trigger ON (the area where the player can open it).
public class Chest : MonoBehaviour
{
    [SerializeField] Key openKey = Key.E;
    [SerializeField] Color openedColor = new Color(0.35f, 0.25f, 0.15f);
    [SerializeField] Vector3 openedScale = new Vector3(1.5f, 0.6f, 1f); // placeholder "lid open" look
    [SerializeField] GameObject prompt; // "Press E" pop-up, shown while the player is next to the closed chest
    public UnityEvent onOpened;

    public bool IsOpen { get; private set; }

    bool playerNear;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (prompt) prompt.SetActive(false);
    }

    void Update()
    {
        if (IsOpen || !playerNear || Keyboard.current == null) return;
        if (Keyboard.current[openKey].wasPressedThisFrame) Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (sr) sr.color = openedColor;
        transform.localScale = openedScale;
        if (prompt) prompt.SetActive(false);
        onOpened?.Invoke();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = true;
        if (prompt && !IsOpen) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = false;
        if (prompt) prompt.SetActive(false);
    }
}

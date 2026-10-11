using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A mast with a small platform (crow's nest) on top that the player can stand on.
// If the player stays up there too long (Camp Time), the boss comes over and hits the mast until it breaks:
// the platform collapses and the player drops. It comes back when the fight restarts.
// Put this on the mast's root. Platform = the crow's nest (with its collider), Parts = everything that falls when it breaks.
public class BreakableMast : MonoBehaviour
{
    public static readonly List<BreakableMast> All = new List<BreakableMast>();

    [SerializeField] Collider2D platform;     // the crow's nest the player stands on
    [SerializeField] Transform[] parts;       // pole, nest and rim: these fall over when it breaks
    [SerializeField] int hitsToBreak = 3;     // breaks easily
    [SerializeField] float campTime = 3f;     // standing on top this long makes the boss come for the mast
    [SerializeField] float fallTime = 0.6f;
    [SerializeField] Color hitFlashColor = new Color(1f, 0.6f, 0.6f);

    public bool Broken { get; private set; }
    public bool PlayerCamping => !Broken && timeOnTop >= campTime;
    public float X => transform.position.x;

    Collider2D playerCol;
    float timeOnTop;
    int hitsLeft;
    Vector3[] startPositions;
    Quaternion[] startRotations;
    Color[] startColors;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        startPositions = new Vector3[parts.Length];
        startRotations = new Quaternion[parts.Length];
        startColors = new Color[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            startPositions[i] = parts[i].position;
            startRotations[i] = parts[i].rotation;
            startColors[i] = parts[i].TryGetComponent(out SpriteRenderer psr) ? psr.color : Color.white;
        }
        hitsLeft = hitsToBreak;
    }

    void Start()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        if (pc) playerCol = pc.GetComponent<Collider2D>();
    }

    void Update()
    {
        if (Broken || !playerCol || !platform) { timeOnTop = 0f; return; }

        // Is the player standing on the crow's nest?
        Bounds p = playerCol.bounds, top = platform.bounds;
        bool onTop = p.min.y >= top.max.y - 0.25f && p.min.y <= top.max.y + 0.6f
                     && p.max.x > top.min.x && p.min.x < top.max.x;
        timeOnTop = onTop ? timeOnTop + Time.deltaTime : 0f;
    }

    // The boss hits the mast
    public void TakeHit()
    {
        if (Broken) return;
        hitsLeft--;
        StartCoroutine(Wobble());
        if (hitsLeft <= 0) StartCoroutine(Break());
    }

    IEnumerator Wobble()
    {
        foreach (Transform t in parts) if (t.TryGetComponent(out SpriteRenderer s)) s.color = hitFlashColor;
        transform.rotation = Quaternion.Euler(0f, 0f, 4f);
        yield return new WaitForSeconds(0.08f);
        transform.rotation = Quaternion.Euler(0f, 0f, -3f);
        yield return new WaitForSeconds(0.08f);
        transform.rotation = Quaternion.identity;
        for (int i = 0; i < parts.Length; i++)
            if (!Broken && parts[i].TryGetComponent(out SpriteRenderer s)) s.color = startColors[i];
    }

    // Platform gone, the mast topples over and fades out
    IEnumerator Break()
    {
        Broken = true;
        timeOnTop = 0f;
        if (platform) platform.enabled = false;
        if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.3f, 0.3f);

        float side = Random.value < 0.5f ? -1f : 1f;
        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].rotation = startRotations[i] * Quaternion.Euler(0f, 0f, side * 80f * k);
                parts[i].position = startPositions[i] + new Vector3(side * 2f * k, -3f * k * k, 0f);
                if (parts[i].TryGetComponent(out SpriteRenderer s)) s.color = new Color(startColors[i].r, startColors[i].g, startColors[i].b, 1f - k);
            }
            yield return null;
        }
        foreach (Transform t in parts) t.gameObject.SetActive(false);
    }

    // Back to standing (when the fight restarts)
    public void Restore()
    {
        StopAllCoroutines();
        Broken = false;
        hitsLeft = hitsToBreak;
        timeOnTop = 0f;
        transform.rotation = Quaternion.identity;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].gameObject.SetActive(true);
            parts[i].position = startPositions[i];
            parts[i].rotation = startRotations[i];
            if (parts[i].TryGetComponent(out SpriteRenderer s)) s.color = startColors[i];
        }
        if (platform) platform.enabled = true;
    }
}

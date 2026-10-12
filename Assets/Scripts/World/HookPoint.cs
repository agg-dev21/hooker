using System.Collections.Generic;
using UnityEngine;

// Hook ring. When the hook hits it, the player is pulled to the ring.
// Needs a Collider2D (Is Trigger ON).
// Shows the level's hook drawing (Resources/HookPointArt) instead of the plain box when there is one.
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

    void Start()
    {
        HookPointArt a = HookPointArt.Current;
        Sprite sprite = a ? a.ForScene() : null;
        if (!sprite) return;

        var go = new GameObject("HookArt");
        go.transform.SetParent(transform, false);
        var art = go.AddComponent<SpriteRenderer>();
        art.sprite = sprite; // its pivot is the middle of the ring's hole, so the hook grabs right there
        if (TryGetComponent(out SpriteRenderer box))
        {
            art.sortingLayerID = box.sortingLayerID;
            art.sortingOrder = box.sortingOrder;
            box.enabled = false;
        }
        Vector3 s = transform.lossyScale; // same size whatever the ring object's scale is
        float sx = Mathf.Abs(s.x) < 0.0001f ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Abs(s.y) < 0.0001f ? 1f : Mathf.Abs(s.y);
        go.transform.localScale = new Vector3(a.drawScale / sx, a.drawScale / sy, 1f);
    }
}

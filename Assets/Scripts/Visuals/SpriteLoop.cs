using UnityEngine;

// Plays a list of sprite frames on this object's SpriteRenderer.
// Loops, or plays once (and can then remove the object). Used by the poison effects.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoop : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f;
    public bool loop = true;
    public bool destroyWhenDone;   // only when not looping

    SpriteRenderer sr;
    float time;

    public static SpriteLoop Add(GameObject go, Sprite[] frames, float fps, bool loop)
    {
        var anim = go.AddComponent<SpriteLoop>();
        anim.frames = frames;
        anim.fps = fps;
        anim.loop = loop;
        return anim;
    }

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        time += Time.deltaTime;
        int i = Mathf.FloorToInt(time * Mathf.Max(fps, 0.01f));
        if (!loop && i >= frames.Length)
        {
            if (destroyWhenDone) Destroy(gameObject);
            else enabled = false;
            return;
        }
        sr.sprite = frames[i % frames.Length];
    }
}

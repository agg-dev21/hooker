using UnityEngine;

// A background picture that always fills the camera's view, far behind everything.
// It slides a little the other way as the camera moves (parallax), so it feels far away.
// Set up by Tools > Art > Tutorial Backgrounds.
[RequireComponent(typeof(SpriteRenderer))]
public class BackdropFollow : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float parallax = 0.08f; // 0 = stuck to the screen, higher = slides more
    [SerializeField] float extraSize = 1.15f;                // a bit bigger than the screen, so the slide never shows an edge

    Camera cam;
    SpriteRenderer sr;
    Vector3 camStart;

    void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        if (cam) camStart = cam.transform.position;
    }

    void LateUpdate()
    {
        if (!cam || !sr.sprite) return;

        // Scale so the picture covers the whole view (plus a margin)
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        Vector2 pic = sr.sprite.bounds.size;
        float scale = Mathf.Max(viewH / pic.y, viewW / pic.x) * extraSize;
        transform.localScale = new Vector3(scale, scale, 1f);

        // Slide against the camera's movement, but never far enough to show an edge
        Vector2 moved = (Vector2)(cam.transform.position - camStart);
        float maxX = (pic.x * scale - viewW) * 0.5f, maxY = (pic.y * scale - viewH) * 0.5f;
        float x = Mathf.Clamp(-moved.x * parallax, -maxX, maxX);
        float y = Mathf.Clamp(-moved.y * parallax, -maxY, maxY);
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(c.x + x, c.y + y, transform.position.z);
    }
}

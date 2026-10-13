using UnityEngine;

// A background picture of a boss arena that stays lined up with the real floor:
// the picture's deck line always sits exactly on the level's deck (Deck Y), it always covers the whole view,
// and it slides a little against the camera as it follows the player (parallax), so it feels far away.
// Set up by Tools > Art > Hardy Arena Background.
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaBackdrop : MonoBehaviour
{
    [SerializeField] float deckY = -5f;                          // world height of the real deck surface
    [SerializeField, Range(0f, 1f)] float deckLine = 0.12f;     // where the deck is in the picture (0 = bottom, 1 = top)
    [SerializeField, Range(0f, 0.5f)] float parallax = 0.06f;   // 0 = stuck to the screen, higher = slides more
    [SerializeField] float extraWidth = 1.12f;                   // a bit wider than the view, so sliding never shows an edge

    Camera cam;
    SpriteRenderer sr;
    float camStartX;

    void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        if (cam) camStartX = cam.transform.position.x;
    }

    void LateUpdate()
    {
        if (!cam || !sr.sprite) return;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        float viewBottom = cam.transform.position.y - viewH * 0.5f;
        float viewTop = viewBottom + viewH;
        Vector2 pic = sr.sprite.bounds.size;

        // Big enough to: cover the width, reach below the bottom of the view, and reach above the top
        float scale = viewW * extraWidth / pic.x;
        scale = Mathf.Max(scale, (deckY - viewBottom + 0.5f) / (pic.y * deckLine));
        scale = Mathf.Max(scale, (viewTop - deckY + 0.5f) / (pic.y * (1f - deckLine)));
        transform.localScale = new Vector3(scale, scale, 1f);

        // Deck line on the real deck; sideways it follows the camera with a small slide
        float h = pic.y * scale;
        float centerY = deckY - deckLine * h + h * 0.5f;
        float maxSlide = Mathf.Max(0f, (pic.x * scale - viewW) * 0.5f);
        float slide = Mathf.Clamp(-(cam.transform.position.x - camStartX) * parallax, -maxSlide, maxSlide);
        transform.position = new Vector3(cam.transform.position.x + slide, centerY, transform.position.z);
    }
}

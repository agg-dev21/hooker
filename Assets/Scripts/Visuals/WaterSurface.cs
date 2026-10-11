using UnityEngine;

// Animated water surface: a row of small pieces along the top edge that bob up and down in a wave.
// Put it on any water box (a SpriteRenderer stretched to the water's size). It adds the surface
// pieces when the game starts, so nothing extra is saved in the scene. Look only (no collider).
public class WaterSurface : MonoBehaviour
{
    [SerializeField] Color surfaceColor = new Color(0.6f, 0.85f, 1f, 0.8f);
    [SerializeField] float pieceWidth = 0.5f;     // width of each surface piece
    [SerializeField] float pieceHeight = 0.12f;   // thickness of the surface line
    [SerializeField] float waveHeight = 0.08f;    // how far the pieces move up and down
    [SerializeField] float waveSpeed = 2f;
    [SerializeField] float waveLength = 3f;       // distance between two wave tops
    [SerializeField] int sortingOrder = 4;

    Transform[] pieces;
    float[] baseY;
    static Sprite white;

    void Start()
    {
        if (!white) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        if (!TryGetComponent(out SpriteRenderer sr)) return;

        Bounds b = sr.bounds;
        int count = Mathf.Max(1, Mathf.CeilToInt(b.size.x / Mathf.Max(pieceWidth, 0.05f)));
        float width = b.size.x / count;
        pieces = new Transform[count];
        baseY = new float[count];

        var holder = new GameObject("Surface").transform; // not a child, so the water box's scale doesn't stretch it
        holder.SetParent(transform.parent, true);
        for (int i = 0; i < count; i++)
        {
            var piece = new GameObject("Wave");
            piece.transform.SetParent(holder);
            piece.transform.position = new Vector3(b.min.x + width * (i + 0.5f), b.max.y, 0f);
            piece.transform.localScale = new Vector3(width * 1.02f, pieceHeight, 1f);
            var psr = piece.AddComponent<SpriteRenderer>();
            psr.sprite = white;
            psr.color = surfaceColor;
            psr.sortingLayerID = sr.sortingLayerID;
            psr.sortingOrder = sortingOrder;
            pieces[i] = piece.transform;
            baseY[i] = b.max.y;
        }
    }

    void Update()
    {
        if (pieces == null) return;
        float k = Mathf.PI * 2f / Mathf.Max(waveLength, 0.1f);
        for (int i = 0; i < pieces.Length; i++)
        {
            Vector3 p = pieces[i].position;
            p.y = baseY[i] + Mathf.Sin(p.x * k + Time.time * waveSpeed) * waveHeight;
            pieces[i].position = p;
        }
    }
}

using UnityEngine;

// Makes a sea picture feel alive. Put it on the ocean object (a repeating SpriteRenderer).
// It adds a darker layer behind and a layer in front (which passes in front of boats, so they sit in the water),
// and every layer drifts sideways at its own speed and bobs up and down. Every number can be changed here.
[RequireComponent(typeof(SpriteRenderer))]
public class OceanLayers : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public float height = 0f;         // up (+) or down (-) from the main sea
        public float drift = -0.6f;       // sideways speed (minus = to the left)
        public float bob = 0.12f;         // how far it moves up and down
        public float bobSpeed = 1.2f;
        public Color tint = Color.white;
        public int sortingOffset = 0;     // drawn this much in front of (+) or behind (-) the main sea
    }

    [SerializeField] Layer main = new Layer { height = 0f, drift = -0.8f, bob = 0.12f, bobSpeed = 1.1f };
    [SerializeField] Layer back = new Layer { height = 0.7f, drift = -0.35f, bob = 0.08f, bobSpeed = 0.8f, tint = new Color(0.7f, 0.7f, 0.85f), sortingOffset = -1 };
    [SerializeField] Layer front = new Layer { height = -1.4f, drift = -1.4f, bob = 0.16f, bobSpeed = 1.5f, tint = new Color(1f, 1f, 1f, 0.9f), sortingOffset = 30 };

    Transform mainT, backT, frontT;
    Vector3 basePos;
    float tileWidth;

    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!sr.sprite) { enabled = false; return; }
        tileWidth = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
        basePos = transform.position;

        // The layers are copies of this sea; this object stays put, the copies move
        mainT = Copy("SeaMain", sr, main);
        backT = Copy("SeaBack", sr, back);
        frontT = Copy("SeaFront", sr, front);
        sr.enabled = false;
    }

    Transform Copy(string name, SpriteRenderer source, Layer layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.drawMode = source.drawMode;
        sr.tileMode = source.tileMode;
        sr.size = source.size + new Vector2(tileWidth * 2f, 0f); // extra width so drifting never shows an edge
        sr.color = layer.tint;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder + layer.sortingOffset;
        return go.transform;
    }

    void Update()
    {
        if (!mainT) return;
        Move(mainT, main, 0f);
        Move(backT, back, 1.7f);
        Move(frontT, front, 3.1f);
    }

    void Move(Transform t, Layer layer, float phase)
    {
        float x = Mathf.Repeat(Time.time * layer.drift, tileWidth);           // drifts, then wraps by one picture width
        float y = layer.height + Mathf.Sin(Time.time * layer.bobSpeed + phase) * layer.bob;
        t.localPosition = new Vector3(x - tileWidth, y, 0f);
    }
}

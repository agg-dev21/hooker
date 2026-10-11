using System.Collections;
using UnityEngine;

// Water dripping from the ceiling: a drop forms, falls and makes a small splash on the floor.
// Look only (harmless). Put it on an empty object at the ceiling; every value can be changed in the Inspector.
public class WaterDrip : MonoBehaviour
{
    [SerializeField] float minWait = 1.2f;        // time between drops (random between min and max)
    [SerializeField] float maxWait = 3f;
    [SerializeField] float dropSize = 0.15f;
    [SerializeField] float gravity = 25f;
    [SerializeField] float maxFall = 20f;         // gives up if no floor is found this far down
    [SerializeField] float splashSize = 0.6f;
    [SerializeField] float splashTime = 0.35f;
    [SerializeField] Color color = new Color(0.6f, 0.85f, 1f, 0.85f);
    [SerializeField] LayerMask groundLayer;      // empty = the "Ground" layer

    static Sprite white;

    void Start()
    {
        if (!white) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        if (groundLayer == 0) groundLayer = LayerMask.GetMask("Ground");
        StartCoroutine(Drip());
    }

    IEnumerator Drip()
    {
        yield return new WaitForSeconds(Random.Range(0f, maxWait)); // drips don't all start together
        while (true)
        {
            // Where it lands
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, maxFall, groundLayer);
            float floorY = hit ? hit.point.y : transform.position.y - maxFall;

            // Forming
            SpriteRenderer drop = Piece("Drop", transform.position, Vector2.zero);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                drop.transform.localScale = Vector3.one * dropSize * (t / 0.4f);
                yield return null;
            }

            // Falling (a bit longer than wide)
            drop.transform.localScale = new Vector3(dropSize, dropSize * 1.6f, 1f);
            float speed = 0f;
            Vector3 p = drop.transform.position;
            while (p.y > floorY)
            {
                speed += gravity * Time.deltaTime;
                p.y -= speed * Time.deltaTime;
                drop.transform.position = p;
                yield return null;
            }
            Destroy(drop.gameObject);
            StartCoroutine(Splash(new Vector2(p.x, floorY)));

            yield return new WaitForSeconds(Random.Range(minWait, maxWait));
        }
    }

    // Flat ring that spreads out, plus two little droplets that hop sideways, all fading out
    IEnumerator Splash(Vector2 at)
    {
        SpriteRenderer ring = Piece("Splash", at, Vector2.zero);
        SpriteRenderer left = Piece("Droplet", at, Vector2.zero);
        SpriteRenderer right = Piece("Droplet", at, Vector2.zero);
        for (float t = 0f; t < splashTime; t += Time.deltaTime)
        {
            float k = t / splashTime;
            ring.transform.localScale = new Vector3(splashSize * k, 0.06f, 1f);
            float hop = Mathf.Sin(k * Mathf.PI) * splashSize * 0.5f;
            left.transform.position = at + new Vector2(-splashSize * 0.5f * k, hop);
            right.transform.position = at + new Vector2(splashSize * 0.5f * k, hop);
            left.transform.localScale = right.transform.localScale = Vector3.one * dropSize * 0.6f;
            Color c = new Color(color.r, color.g, color.b, color.a * (1f - k));
            ring.color = left.color = right.color = c;
            yield return null;
        }
        Destroy(ring.gameObject);
        Destroy(left.gameObject);
        Destroy(right.gameObject);
    }

    SpriteRenderer Piece(string name, Vector2 pos, Vector2 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.color = color;
        sr.sortingOrder = 6;
        return sr;
    }
}

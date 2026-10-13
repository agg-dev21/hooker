using UnityEngine;

// One water wave made by the father (FatherBoss). It rises out of the floor, rolls across the room,
// and soaks the player once if it touches them (see Soaked). Gone when it reaches the far wall.
public class WaterWave : MonoBehaviour
{
    float speed, height, width, endX, riseTime, age;
    float groundY;
    int[] damagePerStack;
    float firstTime, refreshTime;
    bool hitPlayer;
    Transform body, crest;
    BoxCollider2D col;
    Transform art;          // the drawn water (when KK's art is set up)
    float knockback, slowFactor = 1f, slowTime;
    float artScale;

    public void Setup(float dir, float waveSpeed, float waveWidth, float waveHeight, float floorY, float stopX,
                      int[] damage, float soakFirstTime, float soakRefreshTime, Sprite sprite, Color water, Color foam)
    {
        speed = waveSpeed * dir;
        width = waveWidth;
        height = waveHeight;
        groundY = floorY;
        endX = stopX;
        riseTime = 0.25f;
        damagePerStack = damage;
        firstTime = soakFirstTime;
        refreshTime = soakRefreshTime;

        body = Part("Water", sprite, water, 5);
        crest = Part("Foam", sprite, foam, 6);
        col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        var rb = gameObject.AddComponent<Rigidbody2D>(); // so the trigger works while it moves
        rb.bodyType = RigidbodyType2D.Kinematic;
        SetHeight(0.05f);
    }

    // What a hit does besides soaking: pushes the player along with the wave and slows them down
    public void SetHitEffects(float push, float slow, float slowSeconds)
    {
        knockback = push;
        slowFactor = slow;
        slowTime = slowSeconds;
    }

    // Use a drawing instead of the plain boxes. scale = world size of the drawing (same for every wave,
    // so the small frames look small and the big frames look big).
    public void UseArt(Sprite sprite, float scale)
    {
        if (!sprite) return;
        body.gameObject.SetActive(false);
        crest.gameObject.SetActive(false);
        var go = new GameObject("WaterArt");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 6;
        art = go.transform;
        artScale = scale;
        art.localScale = new Vector3(scale, 0.05f, 1f);
    }

    Transform Part(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    // Grows from the floor up
    void SetHeight(float h)
    {
        body.localScale = new Vector3(width, h, 1f);
        body.localPosition = new Vector3(0f, h * 0.5f, 0f);
        crest.localScale = new Vector3(width * 1.15f, Mathf.Min(0.25f, h), 1f);
        crest.localPosition = new Vector3(0f, h - 0.1f, 0f);
        col.size = new Vector2(width * 0.85f, h);
        col.offset = new Vector2(0f, h * 0.5f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float grow = Mathf.Clamp01(age / riseTime);
        SetHeight(Mathf.Lerp(0.05f, height, grow));
        if (art) art.localScale = new Vector3(artScale, artScale * Mathf.Max(grow, 0.05f), 1f); // rises out of the floor
        Vector3 p = transform.position;
        p.x += speed * Time.deltaTime;
        p.y = groundY;
        transform.position = p;
        if ((speed > 0f && p.x >= endX) || (speed < 0f && p.x <= endX)) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) => Touch(other);
    void OnTriggerStay2D(Collider2D other) => Touch(other);

    void Touch(Collider2D other)
    {
        if (hitPlayer || !other.TryGetComponent(out PlayerController2D _)) return;
        if (!Soaked.Hit(other.gameObject, damagePerStack, firstTime, refreshTime)) return;
        hitPlayer = true; // one soak per wave
        if (knockback > 0f && other.attachedRigidbody)
            other.attachedRigidbody.linearVelocity = new Vector2(Mathf.Sign(speed) * knockback, knockback * 0.45f);
        if (slowTime > 0f && other.TryGetComponent(out PlayerController2D pc)) pc.Slow(slowFactor, slowTime);
    }
}

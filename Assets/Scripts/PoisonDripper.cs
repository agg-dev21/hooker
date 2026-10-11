using UnityEngine;

// A leaky pipe in the ceiling that drips poison. Each drop swells at the nozzle (a warning),
// then falls straight down until it hits the ground or the player.
public class PoisonDripper : MonoBehaviour
{
    [SerializeField] float interval = 1.6f;    // seconds between drops
    [SerializeField] float startDelay = 0f;    // offset so drippers next to each other don't drip together
    [SerializeField] float swellTime = 0.45f;  // how long a drop hangs at the nozzle before falling
    [SerializeField] float fallSpeed = 10f;
    [SerializeField] float maxFall = 30f;
    [SerializeField] float dropSize = 0.4f;
    [SerializeField] int poisonDamage = 2;
    [SerializeField] float poisonDuration = 3f;
    [SerializeField] Color dropColor = new Color(0.45f, 1f, 0.2f);
    [SerializeField] LayerMask groundLayer;

    float timer;
    Sprite sprite;

    void Start()
    {
        timer = startDelay;
        if (TryGetComponent(out SpriteRenderer sr)) sprite = sr.sprite;
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer += interval;

        var drop = new GameObject("PoisonDrop");
        drop.transform.position = transform.position + Vector3.down * (transform.lossyScale.y * 0.5f + dropSize * 0.5f);
        drop.transform.localScale = Vector3.one * 0.01f;
        var sr = drop.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = dropColor;
        sr.sortingOrder = 3;
        var col = drop.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        drop.AddComponent<PoisonHazard>().Setup(0, 0f, poisonDamage, poisonDuration, 0.5f, true);
        drop.AddComponent<PoisonDrop>().Setup(dropSize, swellTime, fallSpeed, maxFall, groundLayer);
    }
}

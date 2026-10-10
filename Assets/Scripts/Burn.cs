using UnityEngine;

// Fire: deals damage every few moments. Works on any object with Health.
// No need to add it by hand: call Burn.Apply(object, ...) and it adds itself.
// Getting burned again resets the timer (burns do not stack).
public class Burn : MonoBehaviour
{
    [SerializeField] int tickDamage = 1;
    [SerializeField] float tickInterval = 1f;
    [SerializeField] float duration = 3f;
    [SerializeField] bool showTint = true;   // orange tint while burning (steady, no flashing)
    [SerializeField] Color burnColor = new Color(1f, 0.6f, 0.35f);

    Health health;
    SpriteRenderer sr;
    Color normalColor;
    float timeLeft;
    float tickTimer;

    public static void Apply(GameObject target, int tickDamage, float duration, float tickInterval)
    {
        if (!target.TryGetComponent(out Health _)) return;
        if (!target.TryGetComponent(out Burn burn)) burn = target.AddComponent<Burn>();
        burn.Ignite(tickDamage, duration, tickInterval);
    }

    void Awake()
    {
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        if (sr) normalColor = sr.color;
    }

    public void Ignite(int damage, float time, float interval)
    {
        if (timeLeft <= 0f) tickTimer = interval; // new burn: first tick happens one interval later
        tickDamage = damage;
        duration = time;
        tickInterval = interval;
        timeLeft = time;
    }

    void Update()
    {
        if (timeLeft <= 0f) return;

        timeLeft -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            if (health) health.TakeDamage(tickDamage);
        }

        // Orange while burning, back to normal when it ends
        if (sr && showTint) sr.color = timeLeft > 0f ? burnColor : normalColor;
    }
}

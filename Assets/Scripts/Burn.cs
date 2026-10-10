using UnityEngine;

// Aag: thodi thodi der mein damage. Kisi bhi Health wale object pe lag sakti hai.
// Khud lagane ki zaroorat nahi: Burn.Apply(object, ...) call karo, ye apne aap add ho jaata hai.
// Dobara lagi toh time reset hota hai (stack nahi hoti).
public class Burn : MonoBehaviour
{
    [SerializeField] int tickDamage = 1;
    [SerializeField] float tickInterval = 1f;
    [SerializeField] float duration = 3f;
    [SerializeField] bool showTint = true;   // jalte waqt orange rang (flash nahi, seedha rang)
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
        if (timeLeft <= 0f) tickTimer = interval; // naya burn: pehla tick 1 interval baad
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

        // Jalte waqt orange rang, khatam hone pe normal
        if (sr && showTint) sr.color = timeLeft > 0f ? burnColor : normalColor;
    }
}

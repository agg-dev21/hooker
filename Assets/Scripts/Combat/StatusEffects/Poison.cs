using UnityEngine;

// Poison: deals a little damage every tick for a while and tints the target green.
// Works on any object with Health. No need to add it by hand: call Poison.Apply(object, ...).
// Getting poisoned again refreshes the timer (poison does not stack). Respawning cures it.
public class Poison : MonoBehaviour
{
    static readonly Color PoisonTint = new Color(0.55f, 1f, 0.45f);

    Health health;
    SpriteRenderer sr;
    Color normalColor;
    int tickDamage;
    float tickInterval;
    float timeLeft;
    float tickTimer;
    bool active;
    GameObject effect; // rising poison drips while poisoned

    public bool IsPoisoned => active;

    public static void Apply(GameObject target, int tickDamage, float duration, float tickInterval)
    {
        if (!target.TryGetComponent(out Health _)) return;
        if (!target.TryGetComponent(out Poison poison)) poison = target.AddComponent<Poison>();
        poison.Refresh(tickDamage, duration, tickInterval);
    }

    // Removes the poison right away (antidote, respawn)
    public static void Cure(GameObject target)
    {
        if (target.TryGetComponent(out Poison poison)) poison.Stop();
    }

    void Awake()
    {
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()  => PlayerRespawn.Respawned += OnRespawned;
    void OnDisable() => PlayerRespawn.Respawned -= OnRespawned;

    void OnRespawned()
    {
        if (TryGetComponent(out PlayerRespawn _)) Stop();
    }

    void Refresh(int damage, float duration, float interval)
    {
        if (!active)
        {
            active = true;
            if (sr) normalColor = sr.color;
            tickTimer = interval; // first tick after one interval
            if (!effect) effect = PoisonArt.AddPoisonedEffect(gameObject);
        }
        tickDamage = Mathf.Max(1, damage);
        tickInterval = Mathf.Max(0.05f, interval);
        timeLeft = Mathf.Max(timeLeft, duration);
    }

    void Update()
    {
        if (!active) return;

        timeLeft -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer += tickInterval;
            if (health) health.TakeTickDamage(tickDamage);
        }

        if (timeLeft <= 0f) Stop();
        else if (sr) sr.color = PoisonTint;
    }

    void Stop()
    {
        if (active && sr) sr.color = normalColor;
        if (effect) Destroy(effect);
        effect = null;
        active = false;
        timeLeft = 0f;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 3;
    [SerializeField] float invincibleTime = 0.5f; // seconds after a hit before it can be damaged again
    [SerializeField] bool flashOnHit = true; // flash when hit or not
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.1f; // how long the hit flash lasts

    public UnityEvent onDamaged;
    public UnityEvent onDied;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;

    float invincibleTimer;
    SpriteRenderer sr;
    Color originalColor;

    void Awake()
    {
        CurrentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
        if (sr) originalColor = sr.color;
    }

    void Update()
    {
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
    }

    // Called by the dash: no damage for this long
    public void MakeInvulnerable(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    // The combat system just calls this
    public void TakeDamage(int amount)
    {
        if (invincibleTimer > 0f || CurrentHealth <= 0) return;

        CurrentHealth -= amount;
        invincibleTimer = invincibleTime;
        onDamaged?.Invoke();
        if (sr && flashOnHit) StartCoroutine(Flash());

        if (CurrentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
    }

    void Die()
    {
        onDied?.Invoke();

        // The player respawns; anything else (enemies) is destroyed
        if (TryGetComponent(out PlayerRespawn respawn))
        {
            respawn.Respawn();
            CurrentHealth = maxHealth;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator Flash()
    {
        sr.color = flashColor;
        yield return new WaitForSeconds(flashTime);
        sr.color = originalColor;
    }
}

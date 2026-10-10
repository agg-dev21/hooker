using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 3;
    [SerializeField] float invincibleTime = 0.5f; // hit ke baad itne second tak dobara damage nahi
    [SerializeField] bool flashOnHit = true; // hit lagne pe chamke ya nahi
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.1f; // hit lagne pe kitni der chamke

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

    // Dash isse call karta hai: itni der koi damage nahi
    public void MakeInvulnerable(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    // Combat system bas ye function call karta hai
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

        // Player hai toh respawn, warna (enemy) destroy
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

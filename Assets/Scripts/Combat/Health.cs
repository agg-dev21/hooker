using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 3;
    [SerializeField] float invincibleTime = 0.5f; // seconds after a hit before it can be damaged again
    [SerializeField] bool flashOnHit = true; // flash when hit or not
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.1f; // how long the hit flash lasts

    [Header("Invincibility Frames (player only)")]
    [SerializeField] float playerHitIFrames = 1f;     // after the player is hit: invincible this long (blinking)
    [SerializeField] float playerRespawnIFrames = 1.5f; // after respawning: invincible this long (blinking)
    [SerializeField] float blinkInterval = 0.08f;     // how fast the player blinks while invincible

    public UnityEvent onDamaged;
    public UnityEvent onDied;

    public int CurrentHealth { get; private set; }

    // Item hooks (set by PlayerItems on the player):
    // DamageFilter changes incoming damage (amount, isTick) -> new amount (shield, poison resistance).
    // BeforeDeath can cancel a death by returning true (instant revival).
    public System.Func<int, bool, int> DamageFilter;
    public System.Func<bool> BeforeDeath;
    public int MaxHealth => maxHealth;
    public bool IsInvincible => invincibleTimer > 0f;

    float invincibleTimer;
    float blinkTimer;
    bool isPlayer;
    SpriteRenderer sr;
    readonly List<SpriteRenderer> blinking = new List<SpriteRenderer>(); // sprites that were visible when blinking started
    Color originalColor;

    void Awake()
    {
        CurrentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
        if (sr) originalColor = sr.color;
        isPlayer = GetComponent<PlayerController2D>() != null;
    }

    void Update()
    {
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;

        if (blinkTimer > 0f)
        {
            blinkTimer -= Time.deltaTime;
            // Visible / hidden every blinkInterval; always visible again when the blinking ends
            bool visible = blinkTimer <= 0f || Mathf.Repeat(blinkTimer, blinkInterval * 2f) < blinkInterval;
            SetVisible(visible);
        }
    }

    void OnDisable()
    {
        blinkTimer = 0f;
        SetVisible(true);
    }

    // Called by the dash: no damage for this long (no blinking)
    public void MakeInvulnerable(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    // Invincible for this long and blink so the player can see it
    public void StartIFrames(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);

        if (blinkTimer <= 0f)
        {
            // Only blink the sprites that are currently shown, so hidden ones stay hidden afterwards
            blinking.Clear();
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled) blinking.Add(r);
        }
        blinkTimer = Mathf.Max(blinkTimer, duration);
    }

    // The combat system just calls this
    public void TakeDamage(int amount)
    {
        if (invincibleTimer > 0f || CurrentHealth <= 0) return;
        if (DamageFilter != null) amount = DamageFilter(amount, false);
        if (amount <= 0) return;

        CurrentHealth -= amount;
        invincibleTimer = invincibleTime;
        onDamaged?.Invoke();
        if (sr && flashOnHit) StartCoroutine(Flash());
        if (isPlayer) GameFeel.PlayerHurt(amount, maxHealth); // screen shake

        if (CurrentHealth <= 0) { Die(); return; }

        // Player: invincibility frames after a hit, with blinking
        if (isPlayer) StartIFrames(Mathf.Max(invincibleTime, playerHitIFrames, PlayerBalance.Current.hitInvincibleTime));
    }

    // Damage over time (poison): ignores invincibility frames and does not start them, no flash or blink
    public void TakeTickDamage(int amount)
    {
        if (CurrentHealth <= 0) return;
        if (DamageFilter != null) amount = DamageFilter(amount, true);
        if (amount <= 0) return;
        CurrentHealth -= amount;
        onDamaged?.Invoke();
        if (CurrentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
    }

    void Die()
    {
        if (BeforeDeath != null && BeforeDeath()) return; // revived on the spot

        onDied?.Invoke();

        // The player respawns; anything else (enemies) is destroyed
        if (TryGetComponent(out PlayerRespawn respawn))
        {
            respawn.Respawn();
            CurrentHealth = maxHealth;
            StartIFrames(playerRespawnIFrames); // spawn protection
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

    void SetVisible(bool visible)
    {
        foreach (SpriteRenderer r in blinking)
            if (r) r.enabled = visible;
    }
}
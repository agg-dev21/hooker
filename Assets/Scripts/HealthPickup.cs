using UnityEngine;

// Antidote / health pickup: heals the player, cures poison, then disappears. Bobs up and down.
[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    [SerializeField] int healAmount = 30;
    [SerializeField] bool curesPoison = true;
    [SerializeField] float bobHeight = 0.15f;

    Vector3 start;

    void Start() => start = transform.position;

    void Update()
    {
        transform.position = start + Vector3.up * Mathf.Sin(Time.time * 3f) * bobHeight;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _) || !other.TryGetComponent(out Health health)) return;
        health.Heal(healAmount);
        if (curesPoison) Poison.Cure(other.gameObject);
        Destroy(gameObject);
    }
}

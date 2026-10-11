using UnityEngine;

// One poison drop (made by PoisonDripper). Swells, falls, and pops when it hits the ground.
public class PoisonDrop : MonoBehaviour
{
    float size, swellTime, fallSpeed, fallLeft, age;
    LayerMask groundLayer;
    PoisonHazard hazard;

    public void Setup(float dropSize, float swell, float speed, float maxFall, LayerMask ground)
    {
        size = dropSize;
        swellTime = swell;
        fallSpeed = speed;
        fallLeft = maxFall;
        groundLayer = ground;
        hazard = GetComponent<PoisonHazard>();
        if (hazard) hazard.enabled = false; // harmless while it is still swelling
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age < swellTime)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(0.05f, size, age / swellTime);
            return;
        }
        transform.localScale = Vector3.one * size;
        if (hazard && !hazard.enabled) hazard.enabled = true;

        float step = fallSpeed * Time.deltaTime;
        if (fallLeft <= 0f || Physics2D.Raycast(transform.position, Vector2.down, step + size * 0.5f, groundLayer))
        {
            Destroy(gameObject); // splash
            return;
        }
        transform.position += Vector3.down * step;
        fallLeft -= step;
    }
}

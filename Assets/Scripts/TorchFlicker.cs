using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes a 2D light (and its flame sprite) flicker like a torch
public class TorchFlicker : MonoBehaviour
{
    [SerializeField] Light2D torchLight;
    [SerializeField] float baseIntensity = 1.3f;
    [SerializeField] float flicker = 0.35f;
    [SerializeField] float speed = 7f;

    float seed;

    void Awake()
    {
        seed = Random.value * 100f;
        if (!torchLight) torchLight = GetComponentInChildren<Light2D>();
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        if (torchLight) torchLight.intensity = baseIntensity + (n - 0.5f) * 2f * flicker;
        transform.localScale = new Vector3(transform.localScale.x, Mathf.Lerp(0.5f, 0.7f, n), 1f);
    }
}

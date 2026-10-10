using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Boat pe lagao (ek Collider2D Is Trigger ON chahiye).
// Player boat pe aaya: boat player ko leke right mein chalti hai, screen black hota hai, agla scene load.
public class BoatExit : MonoBehaviour
{
    [SerializeField] string nextScene = "Level_1";
    [SerializeField] float sailSpeed = 6f;
    [SerializeField] float sailTime = 3f;  // kitni der chale
    [SerializeField] float fadeTime = 1f;  // aakhri itne second mein black ho
    [SerializeField] Image fadeImage;      // poore screen ki kaali Image (alpha 0 se shuru)

    bool sailing;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (sailing || !other.TryGetComponent(out PlayerController2D player)) return;
        StartCoroutine(Sail(player));
    }

    IEnumerator Sail(PlayerController2D player)
    {
        sailing = true;

        // Player ab khud nahi chalega, boat ke saath chalega
        player.enabled = false;
        if (player.TryGetComponent(out PlayerCombat combat)) combat.enabled = false;
        if (player.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
        player.transform.SetParent(transform);

        float t = 0f;
        while (t < sailTime)
        {
            t += Time.deltaTime;
            transform.position += Vector3.right * sailSpeed * Time.deltaTime;

            if (fadeImage)
            {
                Color c = fadeImage.color;
                c.a = Mathf.Clamp01((t - (sailTime - fadeTime)) / fadeTime);
                fadeImage.color = c;
            }
            yield return null;
        }

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}

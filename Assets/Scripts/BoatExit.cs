using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put this on the boat (it needs a Collider2D with Is Trigger ON).
// When the player steps on, the boat sails right carrying them, the screen fades to black, and the next scene loads.
public class BoatExit : MonoBehaviour
{
    [SerializeField] string nextScene = "Level_1";
    [SerializeField] float sailSpeed = 6f;
    [SerializeField] float sailTime = 3f;  // how long it sails
    [SerializeField] float fadeTime = 1f;  // fades to black over the last this many seconds
    [SerializeField] Image fadeImage;      // full-screen black Image (starts at alpha 0)

    bool sailing;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (sailing || !other.TryGetComponent(out PlayerController2D player)) return;
        StartCoroutine(Sail(player));
    }

    IEnumerator Sail(PlayerController2D player)
    {
        sailing = true;

        // The player stops moving on their own and rides the boat
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

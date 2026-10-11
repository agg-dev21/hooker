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

    // Where the player stands in the middle of the boat (feet on top of it)
    public static Vector3 MiddleOfBoat(Transform boat, Transform player)
    {
        float top = boat.position.y + 0.5f;
        if (boat.TryGetComponent(out SpriteRenderer bsr)) top = bsr.bounds.max.y;
        float feet = 1f; // from the player's middle down to their feet (from the collider's shape: works with physics off)
        float sy = Mathf.Abs(player.lossyScale.y);
        if (player.TryGetComponent(out BoxCollider2D box)) feet = (box.size.y * 0.5f - box.offset.y) * sy;
        else if (player.TryGetComponent(out CapsuleCollider2D cap)) feet = (cap.size.y * 0.5f - cap.offset.y) * sy;
        return new Vector3(boat.position.x, top + feet + 0.02f, player.position.z);
    }

    Vector3 MiddleOfBoat(PlayerController2D player) => MiddleOfBoat(transform, player.transform);

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
        // Slide to the middle of the boat, feet on its deck, then sail
        Vector3 from = player.transform.position;
        Vector3 to = MiddleOfBoat(player);
        for (float m = 0f; m < 0.3f; m += Time.deltaTime)
        {
            player.transform.position = Vector3.Lerp(from, to, m / 0.3f);
            yield return null;
        }
        player.transform.position = to;
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
            SceneManager.LoadScene(RunOrder.Resolve(nextScene)); // random boss order (see RunOrder)
    }
}

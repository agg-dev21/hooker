using TMPro;
using UnityEngine;

// "Soaked": the father's water waves stack up on the player.
//   1st hit: Soaked 1, a timer starts (First Time, 4 s).
//   Every hit after that: one more stack, more damage, and the timer goes back to Refresh Time (3 s).
//   4th stack: a really heavy hit, then the counter starts over.
//   If the timer runs out before the next hit, the counter goes back to 0.
// A small counter with the time left shows above the player. Added by Soaked.Hit, no setup needed.
// The numbers come from whoever soaks you (FatherBoss), so they are set in the Inspector there.
public class Soaked : MonoBehaviour
{
    Health health;
    int stacks;
    float timeLeft;
    TextMeshPro label;

    public int Stacks => stacks;

    // A wave hit the target. Returns true if it landed (false while dashing / invincible).
    // damagePerStack: damage for the 1st, 2nd, 3rd, 4th... hit; its length is the max stack.
    public static bool Hit(GameObject target, int[] damagePerStack, float firstTime, float refreshTime)
    {
        if (!target.TryGetComponent(out Health h) || h.IsInvincible || h.CurrentHealth <= 0) return false;
        if (damagePerStack == null || damagePerStack.Length == 0) return false;
        if (!target.TryGetComponent(out Soaked soaked)) soaked = target.AddComponent<Soaked>();
        soaked.AddStack(damagePerStack, firstTime, refreshTime);
        return true;
    }

    void Awake()
    {
        health = GetComponent<Health>();
        var go = new GameObject("SoakedCounter");
        go.transform.SetParent(transform, false);
        label = go.AddComponent<TextMeshPro>();
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.55f, 0.85f, 1f);
        label.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        label.GetComponent<MeshRenderer>().sortingOrder = 50;
        label.text = "";
    }

    void OnEnable()  => PlayerRespawn.Respawned += Clear;
    void OnDisable() => PlayerRespawn.Respawned -= Clear;

    void AddStack(int[] damagePerStack, float firstTime, float refreshTime)
    {
        CancelInvoke(nameof(Clear));
        stacks++;
        int damage = damagePerStack[Mathf.Min(stacks, damagePerStack.Length) - 1];
        timeLeft = stacks == 1 ? firstTime : refreshTime;
        if (health) health.TakeDamage(damage);

        if (stacks >= damagePerStack.Length)
        {
            // Final stack: heavy hit landed, start over
            if (CameraFollow2D.Instance) CameraFollow2D.Instance.Shake(0.4f, 0.4f);
            ShowLabel();
            Invoke(nameof(Clear), 0.6f);
            stacks = 0;
            timeLeft = 0f;
            return;
        }
        ShowLabel();
    }

    void Update()
    {
        if (stacks <= 0) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { Clear(); return; }
        ShowLabel();
        // Keep the counter upright and above the head even if the player flips
        Vector3 s = transform.lossyScale;
        label.transform.localScale = new Vector3(1f / (s.x == 0f ? 1f : s.x), 1f / (s.y == 0f ? 1f : s.y), 1f);
        label.transform.localPosition = new Vector3(0f, 1.8f / Mathf.Max(Mathf.Abs(s.y), 0.01f), 0f);
    }

    void ShowLabel()
    {
        if (!label) return;
        label.text = stacks > 0 ? "SOAKED " + stacks + "  " + Mathf.CeilToInt(timeLeft) + "s" : "SOAKED!";
        Vector3 s = transform.lossyScale;
        label.transform.localPosition = new Vector3(0f, 1.8f / Mathf.Max(Mathf.Abs(s.y), 0.01f), 0f);
    }

    void Clear()
    {
        stacks = 0;
        timeLeft = 0f;
        if (label) label.text = "";
    }
}

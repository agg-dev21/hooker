using System.Collections.Generic;
using UnityEngine;

// Holds an enemy in place for a while (the player's Siren's Chain).
// While held, the enemy's own scripts are switched off (it can't move or attack) and it is tinted.
// It can still be hit. Bosses can't be held (they only take the damage).
public class ChainHold : MonoBehaviour
{
    static readonly Color HeldTint = new Color(0.6f, 0.7f, 0.9f);

    float timeLeft;
    readonly List<MonoBehaviour> switchedOff = new List<MonoBehaviour>();
    readonly List<(SpriteRenderer sr, Color color)> tinted = new List<(SpriteRenderer, Color)>();
    Rigidbody2D rb;
    RigidbodyConstraints2D oldConstraints;

    // Returns false if the target can't be held (a boss, or the player)
    public static bool Apply(GameObject target, float seconds)
    {
        if (!target || IsBoss(target) || target.GetComponent<PlayerController2D>()) return false;
        if (target.TryGetComponent(out ChainHold hold)) { hold.timeLeft = Mathf.Max(hold.timeLeft, seconds); return true; }
        hold = target.AddComponent<ChainHold>();
        hold.Begin(seconds);
        return true;
    }

    public static bool IsBoss(GameObject go)
    {
        return go.GetComponentInParent<HardyBoss>() || go.GetComponentInParent<PoisonBoss>()
            || go.GetComponentInParent<OctopusBoss>() || go.GetComponentInParent<FatherBoss>();
    }

    void Begin(float seconds)
    {
        timeLeft = seconds;
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            // Keep health, damage-over-time effects and this script running
            if (mb == this || !mb.enabled || mb is Health || mb is Poison || mb is Burn) continue;
            mb.enabled = false;
            switchedOff.Add(mb);
        }
        if (TryGetComponent(out rb))
        {
            oldConstraints = rb.constraints;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            tinted.Add((sr, sr.color));
            sr.color = sr.color * HeldTint;
        }
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) Destroy(this);
    }

    void OnDestroy()
    {
        foreach (MonoBehaviour mb in switchedOff) if (mb) mb.enabled = true;
        if (rb) rb.constraints = oldConstraints;
        foreach (var t in tinted) if (t.sr) t.sr.color = t.color;
    }
}

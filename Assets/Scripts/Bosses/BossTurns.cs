using System.Collections.Generic;
using UnityEngine;

// When two bosses fight together, they take turns: a boss waits (up to a limit) while the other is
// in the middle of an attack, so the player isn't hit by two big attacks at the same moment.
// A single boss is never slowed down by this.
public static class BossTurns
{
    static readonly HashSet<Object> attacking = new HashSet<Object>();

    public static bool OtherIsAttacking(Object me)
    {
        attacking.RemoveWhere(o => o == null); // a boss that died mid-attack
        foreach (Object o in attacking) if (o != me) return true;
        return false;
    }

    public static void Begin(Object me) => attacking.Add(me);
    public static void End(Object me) => attacking.Remove(me);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => attacking.Clear(); // in case Domain Reload is off
}
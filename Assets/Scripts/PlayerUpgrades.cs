using UnityEngine;

// Permanent upgrades the player earns from bosses (saved, so they stay after quitting).
//   Poison Hook: beat the Level 2 boss (the lizard) and you put his teeth on your hook,
//                so every hook hit also poisons the enemy. Always on from Level 3.
public static class PlayerUpgrades
{
    public const string PoisonHookSave = "upgrade.poisonHook";
    public static bool PoisonHook => PlayerPrefs.GetInt(PoisonHookSave, 0) == 1;

    public static void UnlockPoisonHook()
    {
        if (PoisonHook) return;
        PlayerPrefs.SetInt(PoisonHookSave, 1);
        PlayerPrefs.Save();
    }
}

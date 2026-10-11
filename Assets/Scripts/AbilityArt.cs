using UnityEngine;

// The pictures for the player's special abilities, kept in one asset so every scene can use them
// without setting anything up. Lives at Assets/Resources/AbilityArt.asset
// (Tools > Set Up Hardy Art creates and fills it).
[CreateAssetMenu(fileName = "AbilityArt", menuName = "Hooker/Ability Art")]
public class AbilityArt : ScriptableObject
{
    public Sprite[] hardySpikesFrames; // the attack animation (drawn growing to the left)
    public Sprite hardySpikesIcon;     // the picture in the ability icon
}

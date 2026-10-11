using UnityEngine;

// The on/off switch for the dev tools (Assets/Resources/DevSettings.asset).
// They only ever run in the Unity editor and in Development Builds (Build Profiles > Development Build ticked).
// A normal build never has them. To hide them in the editor too: Tools > Dev Tools > Turn Off (or untick Dev Tools On here).
[CreateAssetMenu(fileName = "DevSettings", menuName = "Hooker/Dev Settings")]
public class DevSettings : ScriptableObject
{
    public bool devToolsOn = true;

    static DevSettings current;
    static bool loaded;

    public static bool On
    {
        get
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return false; // the real game (a normal build): never any dev tools or DEV label, whatever the switch says
#else
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<DevSettings>("DevSettings");
                loaded = true;
            }
            return current && current.devToolsOn;
#endif
        }
    }
}
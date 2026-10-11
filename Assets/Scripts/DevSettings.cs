using UnityEngine;

// The on/off switch for the dev tools (Assets/Resources/DevSettings.asset).
// Turn them OFF before making the final build: Tools > Dev Tools > Turn Off (or untick Dev Tools On here).
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
            if (!loaded || !current) // look again if it wasn't found (Unity keeps old values between plays here)
            {
                current = Resources.Load<DevSettings>("DevSettings");
                loaded = true;
            }
            return current && current.devToolsOn;
        }
    }
}

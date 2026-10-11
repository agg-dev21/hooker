using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Tutorial text that always shows the player's current buttons.
// Write button names in curly brackets, like "{Jump} to jump": it shows the bound key (and the controller
// button when a controller is plugged in), and updates when keys are changed in the pause menu Controls.
// Names: {MoveLeft} {MoveRight} {Jump} {Dash} {Punch} {Hook} {AimUp} {AimDown} {AimLeft} {AimRight}
//        {Interact} {Pause} {Ability} {UseItem} {NextItem} {ChainAbility} {Companion}
// Put it on the same object as a TextMeshPro text.
[RequireComponent(typeof(TMP_Text))]
public class TutorialSign : MonoBehaviour
{
    [TextArea(3, 10)] public string template;

    TMP_Text text;
    float refreshTimer;

    void Awake() => text = GetComponent<TMP_Text>();

    void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.5f;
        text.text = Fill(template);
    }

    public static string Fill(string template)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var sb = new StringBuilder(template);
        bool pad = Gamepad.current != null;
        foreach (GameAction a in System.Enum.GetValues(typeof(GameAction)))
        {
            string token = "{" + a + "}";
            if (template.IndexOf(token, System.StringComparison.Ordinal) < 0) continue;
            string name = GameInput.KeyName(GameInput.GetKey(a)).ToUpper();
            if (pad && GameInput.HasPadBinding(a)) name += " / " + GameInput.PadButtonName(GameInput.GetPadButton(a));
            sb.Replace(token, "<color=#FFD54A>" + name + "</color>");
        }
        return sb.ToString();
    }
}

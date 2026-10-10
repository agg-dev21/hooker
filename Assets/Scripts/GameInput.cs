using UnityEngine;
using UnityEngine.InputSystem;

// One place for all player input: keyboard + mouse + Xbox controller (any gamepad works).
//
//   Action            Keyboard / Mouse          Xbox controller
//   Move              A / D                     Left stick or D-pad
//   Jump              Space                     A
//   Dash              Left Shift                B or LT
//   Punch             X or Left click           X
//   Hook              Z or Right click          RB or RT
//   Aim (hook/punch)  Arrow keys or mouse       Left stick / D-pad (8 directions) or Right stick (free aim)
//   Interact / talk   E                         Y
//   Pause             Esc                       Start (Menu button)
public static class GameInput
{
    const float MoveDeadzone = 0.2f; // left stick: ignore tiny drift when moving
    const float AimDeadzone = 0.5f;  // sticks must be pushed this far to count as aiming

    static Keyboard Kb => Keyboard.current;
    static Gamepad Pad => Gamepad.current;

    // ---------------- Movement ----------------

    // Left/right movement from -1 to 1
    public static float MoveX
    {
        get
        {
            float x = 0f;
            Keyboard kb = Kb;
            if (kb != null)
            {
                if (kb.aKey.isPressed) x -= 1f; // arrow keys are used for aiming
                if (kb.dKey.isPressed) x += 1f;
            }
            float pad = PadMoveX;
            if (pad != 0f) x = pad;
            return Mathf.Clamp(x, -1f, 1f);
        }
    }

    // Controller-only left/right (D-pad first, then left stick). 0 when not pushed.
    public static float PadMoveX
    {
        get
        {
            Gamepad gp = Pad;
            if (gp == null) return 0f;
            float dpad = gp.dpad.x.ReadValue();
            if (Mathf.Abs(dpad) > 0.5f) return Mathf.Sign(dpad);
            float stick = gp.leftStick.x.ReadValue();
            return Mathf.Abs(stick) > MoveDeadzone ? stick : 0f;
        }
    }

    public static bool JumpPressed  => (Kb != null && Kb.spaceKey.wasPressedThisFrame)  || (Pad != null && Pad.buttonSouth.wasPressedThisFrame);
    public static bool JumpReleased => (Kb != null && Kb.spaceKey.wasReleasedThisFrame) || (Pad != null && Pad.buttonSouth.wasReleasedThisFrame);
    public static bool JumpHeld     => (Kb != null && Kb.spaceKey.isPressed)            || (Pad != null && Pad.buttonSouth.isPressed);

    public static bool DashPressed =>
        (Kb != null && Kb.leftShiftKey.wasPressedThisFrame) ||
        (Pad != null && (Pad.buttonEast.wasPressedThisFrame || Pad.leftTrigger.wasPressedThisFrame));

    // ---------------- Combat ----------------

    public static bool PunchKeyPressed => Kb != null && Kb.xKey.wasPressedThisFrame;
    public static bool PunchPadPressed => Pad != null && Pad.buttonWest.wasPressedThisFrame;

    // Keyboard hook (Z) - uses the "Z first, then arrow" waiting logic in PlayerCombat
    public static bool HookKeyPressed => Kb != null && Kb.zKey.wasPressedThisFrame;
    public static bool HookKeyHeld    => Kb != null && Kb.zKey.isPressed;

    // Controller hook (RB / RT) - fires right away in the stick's direction
    public static bool HookPadPressed =>
        Pad != null && (Pad.rightShoulder.wasPressedThisFrame || Pad.rightTrigger.wasPressedThisFrame);

    // ---------------- Aiming ----------------

    // True if the player is aiming with arrows, D-pad or a stick right now
    public static bool AimHeld => TryGetAim(out _);

    // Aim direction from: right stick (free aim), else arrows + left stick + D-pad (8 directions)
    public static bool TryGetAim(out Vector2 dir)
    {
        dir = Vector2.zero;
        Gamepad gp = Pad;

        // Right stick: exact analog direction
        if (gp != null)
        {
            Vector2 right = gp.rightStick.ReadValue();
            if (right.magnitude > AimDeadzone) { dir = right.normalized; return true; }
        }

        Vector2 d = Vector2.zero;

        Keyboard kb = Kb;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed)    d.y += 1f;
            if (kb.downArrowKey.isPressed)  d.y -= 1f;
            if (kb.leftArrowKey.isPressed)  d.x -= 1f;
            if (kb.rightArrowKey.isPressed) d.x += 1f;
        }

        if (gp != null)
        {
            Vector2 left = gp.leftStick.ReadValue();
            if (left.magnitude > AimDeadzone) d += Snap8(left);
            d += gp.dpad.ReadValue();
        }

        if (d.sqrMagnitude < 0.01f) return false; // nothing held (or opposite directions cancelled)
        dir = d.normalized;
        return true;
    }

    // Rounds a direction to the nearest of 8 (up, up-right, right, ...)
    static Vector2 Snap8(Vector2 v)
    {
        float step = Mathf.PI / 4f;
        float angle = Mathf.Round(Mathf.Atan2(v.y, v.x) / step) * step;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // ---------------- Other ----------------

    // Controller interact button (Y). Keyboard keys are set per object (Chest, SecretDoor).
    public static bool InteractPadPressed => Pad != null && Pad.buttonNorth.wasPressedThisFrame;
    public static bool InteractPressed => (Kb != null && Kb.eKey.wasPressedThisFrame) || InteractPadPressed;

    public static bool PausePressed =>
        (Kb != null && Kb.escapeKey.wasPressedThisFrame) || (Pad != null && Pad.startButton.wasPressedThisFrame);
}
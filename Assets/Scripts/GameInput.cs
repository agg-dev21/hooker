using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

// Every action the player can rebind in the pause menu (Controls)
public enum GameAction
{
    MoveLeft, MoveRight, Jump, Dash, Punch, Hook,
    AimUp, AimDown, AimLeft, AimRight, Interact, Pause, Ability
}

// One place for all player input: keyboard + mouse + Xbox controller (any gamepad works).
// Buttons can be changed in the pause menu (Controls) and are saved between sessions.
//
//   Action            Keyboard default        Controller default
//   Move              A / D                   Left stick or D-pad (fixed)
//   Jump              Space                   A
//   Dash              Left Shift              B
//   Punch             X  (+ left click)       X
//   Hook              Z  (+ right click)      RB
//   Aim hook/punch    Arrow keys              Left stick / D-pad (8 directions), right stick (free aim) (fixed)
//   Interact / talk   E                       Y
//   Pause             Esc                     Start (Menu button)
//   Ability           Q                       LB (special ability unlocked from bosses)
public static class GameInput
{
    const float MoveDeadzone = 0.2f; // left stick: ignore tiny drift when moving
    const float AimDeadzone = 0.5f;  // sticks must be pushed this far to count as aiming

    // ---------------- Bindings ----------------

    static readonly Dictionary<GameAction, Key> DefaultKeys = new Dictionary<GameAction, Key>
    {
        { GameAction.MoveLeft, Key.A },        { GameAction.MoveRight, Key.D },
        { GameAction.Jump, Key.Space },        { GameAction.Dash, Key.LeftShift },
        { GameAction.Punch, Key.X },           { GameAction.Hook, Key.Z },
        { GameAction.AimUp, Key.UpArrow },     { GameAction.AimDown, Key.DownArrow },
        { GameAction.AimLeft, Key.LeftArrow }, { GameAction.AimRight, Key.RightArrow },
        { GameAction.Interact, Key.E },        { GameAction.Pause, Key.Escape },
        { GameAction.Ability, Key.Q },
    };

    // Actions that have a controller button (moving and aiming use the sticks / D-pad instead)
    static readonly Dictionary<GameAction, GamepadButton> DefaultPadButtons = new Dictionary<GameAction, GamepadButton>
    {
        { GameAction.Jump, GamepadButton.South },          // A
        { GameAction.Dash, GamepadButton.East },           // B
        { GameAction.Punch, GamepadButton.West },          // X
        { GameAction.Hook, GamepadButton.RightShoulder },  // RB
        { GameAction.Interact, GamepadButton.North },      // Y
        { GameAction.Pause, GamepadButton.Start },         // Start / Menu
        { GameAction.Ability, GamepadButton.LeftShoulder }, // LB
    };

    // Controller buttons that can be picked when rebinding
    public static readonly GamepadButton[] BindablePadButtons =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
        GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.Start, GamepadButton.Select,
        GamepadButton.LeftStick, GamepadButton.RightStick,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
    };

    static readonly Dictionary<GameAction, Key> keys = new Dictionary<GameAction, Key>();
    static readonly Dictionary<GameAction, GamepadButton> padButtons = new Dictionary<GameAction, GamepadButton>();
    static bool loaded;

    public static bool HasPadBinding(GameAction action) => DefaultPadButtons.ContainsKey(action);

    public static Key GetKey(GameAction action) { Load(); return keys[action]; }
    public static GamepadButton GetPadButton(GameAction action) { Load(); return padButtons[action]; }

    // Set a keyboard key. If another action already uses it, the two actions swap keys.
    public static void SetKey(GameAction action, Key key)
    {
        Load();
        GameAction? clash = null;
        foreach (var pair in keys)
            if (pair.Key != action && pair.Value == key) { clash = pair.Key; break; }
        if (clash.HasValue) keys[clash.Value] = keys[action]; // swap
        keys[action] = key;
        Save();
    }

    // Set a controller button. If another action already uses it, the two actions swap buttons.
    public static void SetPadButton(GameAction action, GamepadButton button)
    {
        Load();
        if (!HasPadBinding(action)) return;
        GameAction? clash = null;
        foreach (var pair in padButtons)
            if (pair.Key != action && pair.Value == button) { clash = pair.Key; break; }
        if (clash.HasValue) padButtons[clash.Value] = padButtons[action]; // swap
        padButtons[action] = button;
        Save();
    }

    public static void ResetToDefaults()
    {
        keys.Clear();
        padButtons.Clear();
        foreach (var pair in DefaultKeys) keys[pair.Key] = pair.Value;
        foreach (var pair in DefaultPadButtons) padButtons[pair.Key] = pair.Value;
        loaded = true;
        Save();
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        foreach (var pair in DefaultKeys)
            keys[pair.Key] = (Key)PlayerPrefs.GetInt("bind.key." + pair.Key, (int)pair.Value);
        foreach (var pair in DefaultPadButtons)
            padButtons[pair.Key] = (GamepadButton)PlayerPrefs.GetInt("bind.pad." + pair.Key, (int)pair.Value);
    }

    static void Save()
    {
        foreach (var pair in keys) PlayerPrefs.SetInt("bind.key." + pair.Key, (int)pair.Value);
        foreach (var pair in padButtons) PlayerPrefs.SetInt("bind.pad." + pair.Key, (int)pair.Value);
        PlayerPrefs.Save();
    }

    // ---------------- Reading bound buttons ----------------

    static Keyboard Kb => Keyboard.current;
    static Gamepad Pad => Gamepad.current;

    static KeyControl KeyFor(GameAction action)
    {
        Keyboard kb = Kb;
        if (kb == null) return null;
        Key key = GetKey(action);
        return key == Key.None ? null : kb[key];
    }

    static ButtonControl PadFor(GameAction action)
    {
        Gamepad gp = Pad;
        if (gp == null || !HasPadBinding(action)) return null;
        return gp[GetPadButton(action)];
    }

    public static bool KeyPressed(GameAction a)  { KeyControl k = KeyFor(a); return k != null && k.wasPressedThisFrame; }
    public static bool KeyReleased(GameAction a) { KeyControl k = KeyFor(a); return k != null && k.wasReleasedThisFrame; }
    public static bool KeyHeld(GameAction a)     { KeyControl k = KeyFor(a); return k != null && k.isPressed; }

    public static bool PadPressed(GameAction a)  { ButtonControl b = PadFor(a); return b != null && b.wasPressedThisFrame; }
    public static bool PadReleased(GameAction a) { ButtonControl b = PadFor(a); return b != null && b.wasReleasedThisFrame; }
    public static bool PadHeld(GameAction a)     { ButtonControl b = PadFor(a); return b != null && b.isPressed; }

    public static bool Pressed(GameAction a)  => KeyPressed(a) || PadPressed(a);
    public static bool Released(GameAction a) => KeyReleased(a) || PadReleased(a);
    public static bool Held(GameAction a)     => KeyHeld(a) || PadHeld(a);

    // ---------------- Movement ----------------

    // Left/right movement from -1 to 1
    public static float MoveX
    {
        get
        {
            float x = 0f;
            if (KeyHeld(GameAction.MoveLeft)) x -= 1f;
            if (KeyHeld(GameAction.MoveRight)) x += 1f;
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

    public static bool JumpPressed  => Pressed(GameAction.Jump);
    public static bool JumpReleased => Released(GameAction.Jump);
    public static bool JumpHeld     => Held(GameAction.Jump);
    public static bool DashPressed  => Pressed(GameAction.Dash);
    public static bool AbilityPressed => Pressed(GameAction.Ability);

    // ---------------- Combat ----------------

    public static bool PunchKeyPressed => KeyPressed(GameAction.Punch);
    public static bool PunchPadPressed => PadPressed(GameAction.Punch);

    // Keyboard hook - uses the "hook first, then arrow" waiting logic in PlayerCombat
    public static bool HookKeyPressed => KeyPressed(GameAction.Hook);
    public static bool HookKeyHeld    => KeyHeld(GameAction.Hook);

    // Controller hook - fires right away in the stick's direction
    public static bool HookPadPressed => PadPressed(GameAction.Hook);

    // ---------------- Aiming ----------------

    // True if the player is aiming with the aim keys, D-pad or a stick right now
    public static bool AimHeld => TryGetAim(out _);

    // Aim direction from: right stick (free aim), else aim keys + left stick + D-pad (8 directions)
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
        if (KeyHeld(GameAction.AimUp))    d.y += 1f;
        if (KeyHeld(GameAction.AimDown))  d.y -= 1f;
        if (KeyHeld(GameAction.AimLeft))  d.x -= 1f;
        if (KeyHeld(GameAction.AimRight)) d.x += 1f;

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

    public static bool InteractPadPressed => PadPressed(GameAction.Interact);
    public static bool InteractPressed    => Pressed(GameAction.Interact);
    public static bool PausePressed       => Pressed(GameAction.Pause);

    // ---------------- Names for the menu ----------------

    public static string ActionName(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveLeft:  return "Move Left";
            case GameAction.MoveRight: return "Move Right";
            case GameAction.AimUp:     return "Aim Up";
            case GameAction.AimDown:   return "Aim Down";
            case GameAction.AimLeft:   return "Aim Left";
            case GameAction.AimRight:  return "Aim Right";
            case GameAction.Interact:  return "Interact / Talk";
            case GameAction.Ability:   return "Special Ability";
            default:                   return a.ToString();
        }
    }

    public static string KeyName(Key key)
    {
        if (key == Key.None) return "-";
        Keyboard kb = Kb;
        string name = kb != null ? kb[key].displayName : key.ToString();
        return string.IsNullOrEmpty(name) ? key.ToString() : name;
    }

    // Xbox names for controller buttons
    public static string PadButtonName(GamepadButton b)
    {
        switch (b)
        {
            case GamepadButton.South: return "A";
            case GamepadButton.East: return "B";
            case GamepadButton.West: return "X";
            case GamepadButton.North: return "Y";
            case GamepadButton.LeftShoulder: return "LB";
            case GamepadButton.RightShoulder: return "RB";
            case GamepadButton.LeftTrigger: return "LT";
            case GamepadButton.RightTrigger: return "RT";
            case GamepadButton.Start: return "Menu";
            case GamepadButton.Select: return "View";
            case GamepadButton.LeftStick: return "LS (click)";
            case GamepadButton.RightStick: return "RS (click)";
            case GamepadButton.DpadUp: return "D-pad Up";
            case GamepadButton.DpadDown: return "D-pad Down";
            case GamepadButton.DpadLeft: return "D-pad Left";
            case GamepadButton.DpadRight: return "D-pad Right";
            default: return b.ToString();
        }
    }
}
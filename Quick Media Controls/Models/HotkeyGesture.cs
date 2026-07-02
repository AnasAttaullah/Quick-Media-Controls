using Quick_Media_Controls.Services;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public enum InputType
    {
        Keyboard = 0,
        Mouse = 1
    }

    public readonly struct HotkeyInput
    {
        public InputType Type { get; }
        public Key? Key { get; }
        public MouseButton? MouseButton { get; }

        private HotkeyInput(InputType type, Key? key, MouseButton? mouseButton)
        {
            Type = type;
            Key = key;
            MouseButton = mouseButton;
        }

        public static HotkeyInput FromKey(Key key)
            => new(InputType.Keyboard, key, null);

        public static HotkeyInput FromMouse(MouseButton button)
            => new(InputType.Mouse, null, button);

        public static HotkeyInput Empty()
            => new(0, null, null);

        public string Value()
        {
            return Type switch
            {
                InputType.Keyboard => Key?.ToString() ?? "None",
                InputType.Mouse => MouseButton?.ToString() ?? "None",
                _ => "Unknown"
            };
        }
    }

    public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
    {
        public ModifierKeys modifiers { get; set; }
        public HotkeyInput input { get; set; }

        public HotkeyGesture()
        {
        }

        public HotkeyGesture(ModifierKeys _modifiers, HotkeyInput _input)
        {
            modifiers = _modifiers;
            input = _input;
        }

        public HotkeyGesture Clone() => new(modifiers, input);

        public string ToDisplayString()
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            parts.Add(input.Value());

            return string.Join(" + ", parts);
        }

        public static bool TryFromKeyEvent(KeyEventArgs e, out HotkeyGesture? gesture)
        {
            gesture = null;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = ModifierStateService.GetModifiers();

            if (modifiers == ModifierKeys.None)
                return false;

            if (key is Key.LeftAlt or Key.RightAlt
                or Key.LeftCtrl or Key.RightCtrl
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin
                or Key.None)
            {
                return false;
            }

            gesture = new HotkeyGesture(modifiers, HotkeyInput.FromKey(key));
            return true;
        }

        public static bool TryFromMouseEvent(MouseButtonEventArgs e, out HotkeyGesture? gesture)
        {
            gesture = null;

            var button = e.ChangedButton;
            var modifiers = ModifierStateService.GetModifiers();

            if (modifiers == ModifierKeys.None) return false;

            if (e == null) return false;

            gesture = new HotkeyGesture(modifiers, HotkeyInput.FromMouse(button));

            System.Diagnostics.Debug.WriteLine("This is out gesture: " + gesture.modifiers.ToString() + " " + gesture.input.Value());
            return true;
        }

        public override string ToString()
        {
            return ToDisplayString();
        }
        public bool Equals(HotkeyGesture? other)
        {
            if (other is null) return false;

            return modifiers == other.modifiers
                && input.Type == other.input.Type
                && input.Key == other.input.Key
                && input.MouseButton == other.input.MouseButton;
        }
            
        public override bool Equals(object? obj)
            => obj is HotkeyGesture other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                modifiers,
                input.Type,
                input.Key,
                input.MouseButton
            );
        }
    }
}

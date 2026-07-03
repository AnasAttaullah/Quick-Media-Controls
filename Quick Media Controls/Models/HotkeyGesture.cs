using Quick_Media_Controls.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public enum InputType
    {
        Keyboard = 0,
        Mouse = 1
    }

    public struct HotkeyInput
    {
        public InputType Type { get; set; }
        public Key? Key { get; set; }
        public MouseButton? MouseButton { get; set; }


        [JsonConstructor]
        public HotkeyInput(InputType Type, Key? Key, MouseButton? MouseButton)
        {
            this.Type = Type;
            this.Key = Key;
            this.MouseButton = MouseButton;
        }


        public static HotkeyInput FromKey(Key key)
            => new(InputType.Keyboard, key, null);

        public static HotkeyInput FromMouse(MouseButton button)
            => new(InputType.Mouse, null, button);

     

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
        public ModifierKeys Modifiers { get; set; }
        public HotkeyInput Input { get; set; }



        public HotkeyGesture(ModifierKeys Modifiers, HotkeyInput Input)
        {
            this.Modifiers = Modifiers;
            this.Input = Input;

        }

        public HotkeyGesture Clone()
        {
            return new(Modifiers, Input);
        }

        public string ToDisplayString()
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            parts.Add(Input.Value());

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

        public override string ToString()
        {
            return Modifiers.ToString() + " " + Input.MouseButton.ToString(); 
        }

        public static bool TryFromMouseEvent(MouseButtonEventArgs e, out HotkeyGesture? gesture)
        {
            gesture = null;

            var button = e.ChangedButton;
            var modifiers = ModifierStateService.GetModifiers();

            if (modifiers == ModifierKeys.None) return false;

            if (e == null) return false;

            gesture = new HotkeyGesture(modifiers, HotkeyInput.FromMouse(button));

            return true;
        }


        public bool Equals(HotkeyGesture? other)
        {
            if (other is null) return false;

            return Modifiers == other.Modifiers
                && Input.Type == other.Input.Type
                && Input.Key == other.Input.Key
                && Input.MouseButton == other.Input.MouseButton;
        }
            
        public override bool Equals(object? obj)
            => obj is HotkeyGesture other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Modifiers,
                Input.Type,
                Input.Key,
                Input.MouseButton
            );
        }
    }
}

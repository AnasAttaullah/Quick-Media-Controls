using System.Collections.Generic;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public sealed class HotkeyGesture
    {
        public ModifierKeys Modifiers { get; set; }
        public Key Key { get; set; }

        public HotkeyGesture()
        {
        }

        public HotkeyGesture(ModifierKeys modifiers, Key key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public HotkeyGesture Clone() => new(Modifiers, Key);

        public string ToDisplayString()
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            parts.Add(Key.ToString());

            return string.Join(" + ", parts);
        }

        public static bool TryFromKeyEvent(KeyEventArgs e, out HotkeyGesture? gesture)
        {
            gesture = null;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

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

            gesture = new HotkeyGesture(modifiers, key);
            return true;
        }
    }
}

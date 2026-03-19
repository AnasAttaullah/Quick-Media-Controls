using System.Collections.Generic;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public sealed class AppSettings
    {
        public KeybindSettings Keybinds { get; set; } = KeybindSettings.CreateDefault();

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                Keybinds = KeybindSettings.CreateDefault()
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                Keybinds = Keybinds.Clone()
            };
        }
    }

    public sealed class KeybindSettings
    {
        public HotkeyGesture PlayPause { get; set; } = new(ModifierKeys.Alt, Key.P);
        public HotkeyGesture NextTrack { get; set; } = new(ModifierKeys.Alt, Key.N);
        public HotkeyGesture PreviousTrack { get; set; } = new(ModifierKeys.Alt | ModifierKeys.Shift, Key.P);

        public static KeybindSettings CreateDefault()
        {
            return new KeybindSettings
            {
                PlayPause = new HotkeyGesture(ModifierKeys.Alt, Key.P),
                NextTrack = new HotkeyGesture(ModifierKeys.Alt, Key.N),
                PreviousTrack = new HotkeyGesture(ModifierKeys.Alt | ModifierKeys.Shift, Key.P)
            };
        }

        public KeybindSettings Clone()
        {
            return new KeybindSettings
            {
                PlayPause = PlayPause.Clone(),
                NextTrack = NextTrack.Clone(),
                PreviousTrack = PreviousTrack.Clone()
            };
        }

        public IEnumerable<HotkeyGesture> Enumerate()
        {
            yield return PlayPause;
            yield return NextTrack;
            yield return PreviousTrack;
        }
    }
}

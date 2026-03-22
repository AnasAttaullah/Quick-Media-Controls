using System.Collections.Generic;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public sealed class AppSettings
    {

        public KeybindSettings Keybinds { get; set; } = KeybindSettings.CreateDefault();
        public GeneralSettings General { get; set; } = GeneralSettings.CreateDefault();

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                Keybinds = KeybindSettings.CreateDefault(),
                General = GeneralSettings.CreateDefault()
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                Keybinds = Keybinds.Clone(),
                General = General.Clone()
            };
        }
    }

    public sealed class GeneralSettings
    {
        public bool RunAtStartup { get; set; } = true;
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool AutoHideFlyout { get; set; } = true;
        public bool MoveFlyoutByDefault { get; set; } = false;
        public bool EnableFlyoutAnimations { get; set; } = true;

        public static GeneralSettings CreateDefault()
        {
            return new GeneralSettings
            {
                RunAtStartup = true,
                CheckForUpdatesOnStartup = true,
                AutoHideFlyout = true,
                MoveFlyoutByDefault = false,
                EnableFlyoutAnimations = true
            };
        }

        public GeneralSettings Clone()
        {
            return new GeneralSettings
            {
                RunAtStartup = RunAtStartup,
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                AutoHideFlyout = AutoHideFlyout,
                MoveFlyoutByDefault = MoveFlyoutByDefault,
                EnableFlyoutAnimations = EnableFlyoutAnimations
            };
        }
    }

    public sealed class KeybindSettings
    {
        public HotkeyGesture PlayPause { get; set; } = new(ModifierKeys.Alt, Key.P);
        public HotkeyGesture NextTrack { get; set; } = new(ModifierKeys.Alt, Key.N);
        public HotkeyGesture PreviousTrack { get; set; } = new(ModifierKeys.Alt | ModifierKeys.Shift, Key.P);
        public HotkeyGesture OpenFlyout { get; set; } = new(ModifierKeys.Alt, Key.O);

        public static KeybindSettings CreateDefault()
        {
            return new KeybindSettings
            {
                PlayPause = new HotkeyGesture(ModifierKeys.Alt, Key.P),
                NextTrack = new HotkeyGesture(ModifierKeys.Alt, Key.N),
                PreviousTrack = new HotkeyGesture(ModifierKeys.Alt | ModifierKeys.Shift, Key.P),
                OpenFlyout = new HotkeyGesture(ModifierKeys.Alt, Key.O)
            };
        }

        public KeybindSettings Clone()
        {
            return new KeybindSettings
            {
                PlayPause = PlayPause.Clone(),
                NextTrack = NextTrack.Clone(),
                PreviousTrack = PreviousTrack.Clone(),
                OpenFlyout = OpenFlyout.Clone()
            };
        }

        public IEnumerable<HotkeyGesture> Enumerate()
        {
            yield return PlayPause;
            yield return NextTrack;
            yield return PreviousTrack;
            yield return OpenFlyout;
        }
    }
}

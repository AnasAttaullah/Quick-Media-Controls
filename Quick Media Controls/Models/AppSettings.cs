using System.Collections.Generic;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public sealed class AppSettings
    {
        public GeneralSettings General { get; set; } = GeneralSettings.CreateDefault();
        public KeybindSettings Keybinds { get; set; } = KeybindSettings.CreateDefault();

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                General = GeneralSettings.CreateDefault(),
                Keybinds = KeybindSettings.CreateDefault()
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                General = General.Clone(),
                Keybinds = Keybinds.Clone()
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
        public KeyboardShortcutSettings KeyboardShortcuts { get; set; } = KeyboardShortcutSettings.CreateDefault();
        public MouseShortcutSettings MouseShortcuts { get; set; } = MouseShortcutSettings.CreateDefault();

        public static KeybindSettings CreateDefault()
        {
            return new KeybindSettings
            {
                KeyboardShortcuts = KeyboardShortcutSettings.CreateDefault(),
                MouseShortcuts = MouseShortcutSettings.CreateDefault()
            };
        }

        public KeybindSettings Clone()
        {
            return new KeybindSettings
            {
                KeyboardShortcuts = KeyboardShortcuts.Clone(),
                MouseShortcuts = MouseShortcuts.Clone()
            };
        }
    }

    public sealed class KeyboardShortcutSettings
    {
        public HotkeyGesture PlayPause { get; set; } = new(ModifierKeys.Alt, Key.P);
        public HotkeyGesture NextTrack { get; set; } = new(ModifierKeys.Alt, Key.N);
        public HotkeyGesture PreviousTrack { get; set; } = new(ModifierKeys.Alt | ModifierKeys.Shift, Key.P);
        public HotkeyGesture OpenFlyout { get; set; } = new(ModifierKeys.Alt, Key.O);

        public static KeyboardShortcutSettings CreateDefault()
        {
            return new KeyboardShortcutSettings
            {
                PlayPause = new HotkeyGesture(ModifierKeys.Alt, Key.P),
                NextTrack = new HotkeyGesture(ModifierKeys.Alt, Key.N),
                PreviousTrack = new HotkeyGesture(ModifierKeys.Alt | ModifierKeys.Shift, Key.P),
                OpenFlyout = new HotkeyGesture(ModifierKeys.Alt, Key.O)
            };
        }

        public KeyboardShortcutSettings Clone()
        {
            return new KeyboardShortcutSettings
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

    public sealed class MouseShortcutSettings
    {
        public ShortcutAction? LeftClick { get; set; } = ShortcutAction.PlayPause;
        public ShortcutAction? DoubleLeftClick { get; set; } = ShortcutAction.NextTrack;
        public ShortcutAction? RightClick { get; set; } = ShortcutAction.OpenFlyout;
        public ShortcutAction? MiddleClick { get; set; } = null;

        public static MouseShortcutSettings CreateDefault()
        {
            return new MouseShortcutSettings
            {
                LeftClick = ShortcutAction.PlayPause,
                DoubleLeftClick = ShortcutAction.NextTrack,
                RightClick = ShortcutAction.OpenFlyout,
                MiddleClick = null
            };
        }

        public MouseShortcutSettings Clone()
        {
            return new MouseShortcutSettings
            {
                LeftClick = LeftClick,
                DoubleLeftClick = DoubleLeftClick,
                RightClick = RightClick,
                MiddleClick = MiddleClick
            };
        }

        public IEnumerable<ShortcutAction?> Enumerate()
        {
            yield return LeftClick;
            yield return DoubleLeftClick;
            yield return RightClick;
            yield return MiddleClick;
        }
    }
}

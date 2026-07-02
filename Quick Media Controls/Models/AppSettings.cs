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
        public bool StartupRegistrationInitialized { get; set; } = false;
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool AutoHideFlyout { get; set; } = true;
        public bool MoveFlyoutByDefault { get; set; } = false;
        public bool EnableFlyoutAnimations { get; set; } = true;

        public static GeneralSettings CreateDefault()
        {
            return new GeneralSettings
            {
                RunAtStartup = true,
                StartupRegistrationInitialized = false,
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
                StartupRegistrationInitialized = StartupRegistrationInitialized,
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                AutoHideFlyout = AutoHideFlyout,
                MoveFlyoutByDefault = MoveFlyoutByDefault,
                EnableFlyoutAnimations = EnableFlyoutAnimations
            };
        }
    }

    public sealed class KeybindSettings
    {
        public ShortcutSettings Shortcuts { get; set; } = ShortcutSettings.CreateDefault();
        public IconShortcutSettings IconShortcuts { get; set; } = IconShortcutSettings.CreateDefault();

        public static KeybindSettings CreateDefault()
        {
            return new KeybindSettings
            {
                Shortcuts = ShortcutSettings.CreateDefault(),
                IconShortcuts = IconShortcutSettings.CreateDefault()
            };
        }

        public KeybindSettings Clone()
        {
            return new KeybindSettings
            {
                Shortcuts = Shortcuts.Clone(),
                IconShortcuts = IconShortcuts.Clone()
            };
        }
    }

    public sealed class ShortcutSettings
    {
        public HotkeyGesture PlayPause { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton1));
        public HotkeyGesture NextTrack { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromKey(Key.N));
        public HotkeyGesture PreviousTrack { get; set; } = new(ModifierKeys.Alt | ModifierKeys.Shift, HotkeyInput.FromKey(Key.P));
        public HotkeyGesture OpenFlyout { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromKey(Key.O));

        public static ShortcutSettings CreateDefault()
        {
            return new ShortcutSettings
            {
                PlayPause = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton1)),
                NextTrack = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromKey(Key.N)),
                PreviousTrack = new HotkeyGesture(ModifierKeys.Alt | ModifierKeys.Shift, HotkeyInput.FromKey(Key.P)),
                OpenFlyout = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromKey(Key.O))
            };
        }

        public ShortcutSettings Clone()
        {
            return new ShortcutSettings
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

    public sealed class IconShortcutSettings
    {
        public ShortcutAction? LeftClick { get; set; } = ShortcutAction.PlayPause;
        public ShortcutAction? DoubleLeftClick { get; set; } = ShortcutAction.NextTrack;
        public ShortcutAction? RightClick { get; set; } = ShortcutAction.OpenFlyout;
        public ShortcutAction? MiddleClick { get; set; } = null;

        public static IconShortcutSettings CreateDefault()
        {
            return new IconShortcutSettings
            {
                LeftClick = ShortcutAction.PlayPause,
                DoubleLeftClick = ShortcutAction.NextTrack,
                RightClick = ShortcutAction.OpenFlyout,
                MiddleClick = null
            };
        }

        public IconShortcutSettings Clone()
        {
            return new IconShortcutSettings
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

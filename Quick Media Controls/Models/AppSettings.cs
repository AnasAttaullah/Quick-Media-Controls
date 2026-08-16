using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Quick_Media_Controls.Models
{
    public enum ApplicationThemeSetting
    {
        System,
        Light,
        Dark
    }

    public enum TrayIconThemeSetting
    {
        System,
        Light,
        Dark
    }

    public sealed class AppSettings
    {
        public GeneralSettings General { get; set; } = GeneralSettings.CreateDefault();
        public ThemeSettings Theme { get; set; } = ThemeSettings.CreateDefault();
        public KeybindSettings Keybinds { get; set; } = KeybindSettings.CreateDefault();

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                General = GeneralSettings.CreateDefault(),
                Theme = ThemeSettings.CreateDefault(),
                Keybinds = KeybindSettings.CreateDefault()
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                General = General.Clone(),
                Theme = Theme.Clone(),
                Keybinds = Keybinds.Clone()
            };
        }
    }

    public sealed class ThemeSettings
    {
        public ApplicationThemeSetting AppTheme { get; set; } = ApplicationThemeSetting.System;
        public TrayIconThemeSetting TrayIconTheme { get; set; } = TrayIconThemeSetting.System;

        public static ThemeSettings CreateDefault()
        {
            return new ThemeSettings
            {
                AppTheme = ApplicationThemeSetting.System,
                TrayIconTheme = TrayIconThemeSetting.System
            };
        }

        public ThemeSettings Clone()
        {
            return new ThemeSettings
            {
                AppTheme = AppTheme,
                TrayIconTheme = TrayIconTheme
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
        public double? FlyoutPositionX { get; set; } = null;
        public double? FlyoutPositionY { get; set; } = null;

        public static GeneralSettings CreateDefault()
        {
            return new GeneralSettings
            {
                RunAtStartup = true,
                StartupRegistrationInitialized = false,
                CheckForUpdatesOnStartup = true,
                AutoHideFlyout = true,
                MoveFlyoutByDefault = false,
                EnableFlyoutAnimations = true,
                FlyoutPositionX = null,
                FlyoutPositionY = null
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
                EnableFlyoutAnimations = EnableFlyoutAnimations,
                FlyoutPositionX = FlyoutPositionX,
                FlyoutPositionY = FlyoutPositionY
            };
        }
    }

    public sealed class KeybindSettings
    {
        public ShortcutSettings Shortcuts { get; set; } = ShortcutSettings.CreateDefault();
        public TrayIconShortcutSettings TrayIconShortcuts { get; set; } = TrayIconShortcutSettings.CreateDefault();

        [JsonPropertyName("KeyboardShortcuts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ShortcutSettings? LegacyKeyboardShortcuts
        {
            get => null;
            set { if (value != null) Shortcuts = value; }
        }

        [JsonPropertyName("MouseShortcuts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TrayIconShortcutSettings? LegacyMouseShortcuts
        {
            get => null;
            set { if (value != null) TrayIconShortcuts = value; }
        }

        [JsonPropertyName("IconShortcuts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TrayIconShortcutSettings? LegacyIconShortcuts
        {
            get => null;
            set { if (value != null) TrayIconShortcuts = value; }
        }

        public static KeybindSettings CreateDefault()
        {
            return new KeybindSettings
            {
                Shortcuts = ShortcutSettings.CreateDefault(),
                TrayIconShortcuts = TrayIconShortcutSettings.CreateDefault()
            };
        }

        public KeybindSettings Clone()
        {
            return new KeybindSettings
            {
                Shortcuts = Shortcuts.Clone(),
                TrayIconShortcuts = TrayIconShortcuts.Clone()
            };
        }
    }

    public sealed class ShortcutSettings
    {
        public HotkeyGesture PlayPause { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromKey(Key.P));
        public HotkeyGesture? PlayPauseSecondary { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.Right));

        public HotkeyGesture NextTrack { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromKey(Key.N));
        public HotkeyGesture? NextTrackSecondary { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton2));

        public HotkeyGesture PreviousTrack { get; set; } = new(ModifierKeys.Alt | ModifierKeys.Shift, HotkeyInput.FromKey(Key.P));
        public HotkeyGesture? PreviousTrackSecondary { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton1));

        public HotkeyGesture OpenFlyout { get; set; } = new(ModifierKeys.Alt, HotkeyInput.FromKey(Key.O));
        public HotkeyGesture? OpenFlyoutSecondary { get; set; } = null;

        public static ShortcutSettings CreateDefault()
        {
            return new ShortcutSettings
            { 
                PlayPause = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromKey(Key.P)),
                PlayPauseSecondary = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.Right)),
                NextTrack = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromKey(Key.N)),
                NextTrackSecondary = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton2)),
                PreviousTrack = new HotkeyGesture(ModifierKeys.Alt | ModifierKeys.Shift, HotkeyInput.FromKey(Key.P)),
                PreviousTrackSecondary = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromMouse(MouseButton.XButton1)),
                OpenFlyout = new HotkeyGesture(ModifierKeys.Alt, HotkeyInput.FromKey(Key.O)),
                OpenFlyoutSecondary = null
            };
        }

        public ShortcutSettings Clone()
        {
            return new ShortcutSettings
            {
                PlayPause = PlayPause.Clone(),
                PlayPauseSecondary = PlayPauseSecondary?.Clone(),
                NextTrack = NextTrack.Clone(),
                NextTrackSecondary = NextTrackSecondary?.Clone(),
                PreviousTrack = PreviousTrack.Clone(),
                PreviousTrackSecondary = PreviousTrackSecondary?.Clone(),
                OpenFlyout = OpenFlyout.Clone(),
                OpenFlyoutSecondary = OpenFlyoutSecondary?.Clone()
            };
        }

        public IEnumerable<HotkeyGesture> Enumerate()
        {
            if (PlayPause != null) yield return PlayPause;
            if (PlayPauseSecondary != null) yield return PlayPauseSecondary;
            if (NextTrack != null) yield return NextTrack;
            if (NextTrackSecondary != null) yield return NextTrackSecondary;
            if (PreviousTrack != null) yield return PreviousTrack;
            if (PreviousTrackSecondary != null) yield return PreviousTrackSecondary;
            if (OpenFlyout != null) yield return OpenFlyout;
            if (OpenFlyoutSecondary != null) yield return OpenFlyoutSecondary;
        }
    }

    public sealed class TrayIconShortcutSettings
    {
        public ShortcutAction? LeftClick { get; set; } = ShortcutAction.PlayPause;
        public ShortcutAction? DoubleLeftClick { get; set; } = ShortcutAction.NextTrack;
        public ShortcutAction? RightClick { get; set; } = ShortcutAction.OpenFlyout;
        public ShortcutAction? MiddleClick { get; set; } = null;

        public static TrayIconShortcutSettings CreateDefault()
        {
            return new TrayIconShortcutSettings
            {
                LeftClick = ShortcutAction.PlayPause,
                DoubleLeftClick = ShortcutAction.NextTrack,
                RightClick = ShortcutAction.OpenFlyout,
                MiddleClick = null
            };
        }

        public TrayIconShortcutSettings Clone()
        {
            return new TrayIconShortcutSettings
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

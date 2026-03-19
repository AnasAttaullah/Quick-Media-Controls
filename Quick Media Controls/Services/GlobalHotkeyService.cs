using Quick_Media_Controls.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Quick_Media_Controls.Services
{
    public enum GlobalHotkeyAction
    {
        PlayPause = 1,
        NextTrack = 2,
        PreviousTrack = 3,
        OpenFlyout = 4
    }

    public sealed class GlobalHotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly IntPtr _windowHandle;
        private readonly HwndSource _hwndSource;
        private readonly Dictionary<int, GlobalHotkeyAction> _registeredHotkeyActions = new Dictionary<int, GlobalHotkeyAction>();
        private bool _isDisposed;

        public event EventHandler<GlobalHotkeyAction>? HotkeyPressed;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkeyService(Window messageWindow)
        {
            _windowHandle = new WindowInteropHelper(messageWindow).Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource.AddHook(WndProc);
        }

        public void Apply(KeybindSettings settings)
        {
            UnregisterAll();

            ValidateNoDuplicates(settings);

            Register(1001, settings.PlayPause, GlobalHotkeyAction.PlayPause);
            Register(1002, settings.NextTrack, GlobalHotkeyAction.NextTrack);
            Register(1003, settings.PreviousTrack, GlobalHotkeyAction.PreviousTrack);
            Register(1004, settings.OpenFlyout, GlobalHotkeyAction.OpenFlyout);
        }

        private void Register(int id, HotkeyGesture gesture,GlobalHotkeyAction action)
        {
            var modifiers = ToNativeModifiers(gesture.Modifiers) | MOD_NOREPEAT;
            var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);

            if(!RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey. Error code: {errorCode}");
            }
            _registeredHotkeyActions[id] = action;
        }

        public void UnregisterAll()
        {
            foreach(var id in _registeredHotkeyActions.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _registeredHotkeyActions.Clear();
        }

        private static void ValidateNoDuplicates(KeybindSettings settings)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hotkey in settings.Enumerate())
            {
                var key = $"{(int)hotkey.Modifiers}:{(int)hotkey.Key}";
                if (!set.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate hotkey detected: {hotkey.ToDisplayString()}");
                }
            }
        }

        private static uint ToNativeModifiers(ModifierKeys modifiers)
        {
            uint native = 0;

            if(modifiers.HasFlag(ModifierKeys.Alt)) native |= MOD_ALT;
            if(modifiers.HasFlag(ModifierKeys.Control)) native |= MOD_CONTROL;
            if(modifiers.HasFlag(ModifierKeys.Shift)) native |= MOD_SHIFT;
            if(modifiers.HasFlag(ModifierKeys.Windows)) native |= MOD_WIN;

            return native;
        }

        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (_isDisposed)
                return IntPtr.Zero;

            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (_registeredHotkeyActions.TryGetValue(id, out var action))
                {
                    HotkeyPressed?.Invoke(this, action);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            UnregisterAll();
            _hwndSource.RemoveHook(WndProc);
            HotkeyPressed = null;

            GC.SuppressFinalize(this);
        }
    }
}

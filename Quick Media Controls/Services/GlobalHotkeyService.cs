using AutoUpdaterDotNET;
using Microsoft.VisualBasic.Devices;
using Quick_Media_Controls.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace Quick_Media_Controls.Services
{
    public sealed class GlobalHotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        // Native Windows constants for the low-level mouse hook
        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONUP = 0x0207;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly IntPtr _windowHandle;
        private readonly HwndSource _hwndSource;
        private readonly Dictionary<int, ShortcutAction> _registeredHotkeyActions = new();
        private readonly Dictionary<HotkeyGesture, ShortcutAction> _registeredMouseHotkeys = new();
        private bool _isDisposed;

        // Keeps a reference to the delegate so it doesn't get garbage collected
        private LowLevelMouseProc _mouseProc;
        private static IntPtr _mouseHookHandle = IntPtr.Zero;
        public event EventHandler<ShortcutAction>? HotkeyPressed;

        // --- Win32 Imports ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public GlobalHotkeyService(Window messageWindow)
        {
            // Initialize and register the low-level mouse hook
            _mouseProc = MouseHookCallback;
            using (ProcessModule curModule = Process.GetCurrentProcess().MainModule!)
            {
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
            }
            Debug.WriteLine("Started!!!!!!!!!!!!!!!!!!!!!!!!!!!");

            _windowHandle = new WindowInteropHelper(messageWindow).Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource.AddHook(WndProc);

            
        }

        public void Apply(KeybindSettings settings)
        {
            UnregisterAll();

            ShortcutSettings keyboard = settings.Shortcuts ?? ShortcutSettings.CreateDefault();
            ValidateNoDuplicates(keyboard);

            try
            {
                Register(1001, keyboard.PlayPause, ShortcutAction.PlayPause);
                Register(1002, keyboard.NextTrack, ShortcutAction.NextTrack);
                Register(1003, keyboard.PreviousTrack, ShortcutAction.PreviousTrack);
                Register(1004, keyboard.OpenFlyout, ShortcutAction.OpenFlyout);
            }
            catch
            {
                UnregisterAll();

                throw new InvalidOperationException(
                    "One or more hotkeys are already in use by another application. " +
                    "Please change your hotkeys and try again.");
            }
        }

        private void Register(int id, HotkeyGesture gesture, ShortcutAction action)
        {
            //If input type is keyboard then input.key *shouddnt* be null 
            if (gesture.Input.Type == Models.InputType.Keyboard)
            {
                if (gesture.Input.Key == null) return;

                var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Input.Key.Value);
                var modifiers = ToNativeModifiers(gesture.Modifiers) | MOD_NOREPEAT;

                if (!RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to register hotkey. Error code: {errorCode}");
                }

                Debug.WriteLine("Added a keyboard gesture");
                _registeredHotkeyActions[id] = action;
            }
            else
            {
                Debug.WriteLine("Added a mouse gesture");
                _registeredMouseHotkeys[gesture] = action;

                Debug.WriteLine(gesture.Input.MouseButton);
            }
        }

        public void UnregisterAll()
        {
            foreach (var id in _registeredHotkeyActions.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }

            _registeredHotkeyActions.Clear();
            _registeredMouseHotkeys.Clear();
        }

        private static void ValidateNoDuplicates(ShortcutSettings settings)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hotkey in settings.Enumerate())
            {
                if (hotkey.Input.Key == null) continue;
                var key = $"{(int)hotkey.Modifiers}:{(int)hotkey.Input.Key}";
                if (!set.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate hotkey detected: {hotkey.ToDisplayString()}");
                }
            }
        }

        private static uint ToNativeModifiers(ModifierKeys modifiers)
        {
            uint native = 0;

            if (modifiers.HasFlag(ModifierKeys.Alt)) native |= MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Control)) native |= MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Shift)) native |= MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Windows)) native |= MOD_WIN;

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

        // Handles the global mouse event streaming interception
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

            if (!TryGetMouseGesture(wParam, lParam, out var gesture))
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

            if (_registeredMouseHotkeys.TryGetValue(gesture, out var action))
            {
                HotkeyPressed?.Invoke(this, action);
                return (IntPtr)1;
            }

            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }
        
        private static bool TryGetMouseGesture(IntPtr wParam, IntPtr lParam, out HotkeyGesture gesture)
        {
            gesture = default;

            int message = wParam.ToInt32();

            var mods = ModifierStateService.GetModifiers();

            MouseButton? button = message switch
            {
                WM_LBUTTONUP => MouseButton.Left,
                WM_RBUTTONUP => MouseButton.Right,
                WM_MBUTTONUP => MouseButton.Middle,
                WM_XBUTTONUP => GetXButton(lParam),
                _ => null
            };


            if (button is null)
             return false;

            gesture = new HotkeyGesture(mods, HotkeyInput.FromMouse(button.Value));
            return true;
        }

        private static MouseButton GetXButton(IntPtr lParam)
        {
            var hook = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            uint button = (hook.mouseData >> 16) & 0xFFFF;

            return button switch
            {
                1 => MouseButton.XButton1,
                2 => MouseButton.XButton2
            };
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            UnregisterAll();

            // Safely tear down the global mouse hook
            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
            }

            _hwndSource.RemoveHook(WndProc);
            HotkeyPressed = null;

            GC.SuppressFinalize(this);
        }
    }
}

using Microsoft.VisualBasic.Devices;
using Quick_Media_Controls.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Quick_Media_Controls.Services
{
    public sealed class GlobalHotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        // Native Windows constants for the low-level mouse hook
        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int XBUTTON2 = 0x0002;
        private const int VK_CONTROL = 0x11;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly IntPtr _windowHandle;
        private readonly HwndSource _hwndSource;
        private readonly Dictionary<int, ShortcutAction> _registeredHotkeyActions = new();
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

            var keyboard = settings.KeyboardShortcuts ?? KeyboardShortcutSettings.CreateDefault();
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
            if (gesture.input.Type != Models.InputType.Keyboard) return;
            if (gesture.input.Key == null) return;

            var modifiers = ToNativeModifiers(gesture.modifiers) | MOD_NOREPEAT;

            //
            var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.input.Key.Value);

            if (!RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey. Error code: {errorCode}");
            }

            _registeredHotkeyActions[id] = action;
        }

        public void UnregisterAll()
        {
            foreach (var id in _registeredHotkeyActions.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }

            _registeredHotkeyActions.Clear();
        }

        private static void ValidateNoDuplicates(KeyboardShortcutSettings settings)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hotkey in settings.Enumerate())
            {
                if (hotkey.input.Key == null) continue;
                var key = $"{(int)hotkey.modifiers}:{(int)hotkey.input.Key}";
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
        
       /* private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if ((info.flags & 0x01) != 0) // LLMHF_INJECTED
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

                if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    return (IntPtr)1;
                }
                return (IntPtr)1;
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }*/
       /* 
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

            switch ((int)wParam)
            {
                case WM_RBUTTONDOWN:
                    Debug.WriteLine("Blocked right click");
                    return (IntPtr)1;

                default:
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
            }
        }
        */
        // Handles the global mouse event streaming interception
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (wParam.ToInt32() != WM_XBUTTONDOWN)
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

            if (!_isDisposed && nCode >= 0 && (int)wParam == WM_XBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int mouseData = (short)((hookStruct.mouseData >> 16) & 0xffff);

                uint xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                bool isCtrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool isTMB2Pressed = xButton == 2;
                if (isCtrlPressed & isTMB2Pressed)
                {
                    // Fire the play/pause action (or change this action to NextTrack, OpenFlyout, etc.)
                    HotkeyPressed?.Invoke(this, ShortcutAction.PlayPause);

                    // Return 1 to prevent Windows from passing this click to background apps
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
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

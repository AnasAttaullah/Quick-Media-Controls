using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Quick_Media_Controls.Services
{
    internal static class ModifierStateService
    {
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public static ModifierKeys GetModifiers()
        {
            ModifierKeys mods = ModifierKeys.None;

            const int VK_SHIFT   = 0x10;
            const int VK_CONTROL = 0x11;
            const int VK_MENU    = 0x12; // Alt key (left or right)
            const int VK_LWIN    = 0x5B; // Left Windows key
            const int VK_RWIN    = 0x5C; // Right Windows key

            if ((GetKeyState(VK_CONTROL) & 0x8000) != 0)
                mods |= ModifierKeys.Control;

            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0)
                mods |= ModifierKeys.Shift;

            if ((GetKeyState(VK_MENU) & 0x8000) != 0)
                mods |= ModifierKeys.Alt;

            if ((GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0)
                mods |= ModifierKeys.Windows;

            return mods;
        }
    }
}

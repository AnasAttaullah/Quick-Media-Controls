
using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.System;

namespace Quick_Media_Controls.Services
{
    internal static class ModifierStateService
    {
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public static ModifierKeys GetModifiers()
        {
            ModifierKeys mods = ModifierKeys.None;
            var VK_CONTROL = 0x11;
            var VK_SHIFT = 0x10;
            var VK_LMENU = 0x12;


            if ((GetKeyState(VK_CONTROL) & 0x8000) != 0)
                mods |= ModifierKeys.Control;

            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0)
                mods |= ModifierKeys.Shift;

            if ((GetKeyState(VK_LMENU) & 0x8000) != 0)
                mods |= ModifierKeys.Alt;

            return mods;
        }
    }
}

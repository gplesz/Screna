using System;
using System.Runtime.InteropServices;

namespace Screna.Native
{
    static class DWMApi
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hWnd, int dWAttribute, ref RECT pvAttribute, int cbAttribute);
    }
}

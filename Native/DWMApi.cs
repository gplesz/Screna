using System;
using System.Runtime.InteropServices;

namespace Screna.Native
{
    enum DwmWindowAttribute { ExtendedFrameBounds }

    static class DWMApi
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hWnd, DwmWindowAttribute dWAttribute, ref RECT pvAttribute, int cbAttribute);
    }
}

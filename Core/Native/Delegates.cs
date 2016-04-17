using System;

namespace Screna.Native
{
    delegate IntPtr WindowProcedureHandler(IntPtr hwnd, uint uMsg, IntPtr wparam, IntPtr lparam);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
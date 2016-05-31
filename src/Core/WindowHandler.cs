using Screna.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Screna
{
    /// <summary>
    /// A Class for Enumerating Windows.
    /// </summary>
    public class WindowHandler
    {
        #region PInvoke
        const string DllName = "user32.dll";

        [DllImport(DllName)]
        static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport(DllName)]
        static extern int GetWindowText(IntPtr hWnd, [Out] StringBuilder lpString, int nMaxCount);

        [DllImport(DllName)]
        static extern IntPtr GetWindow(IntPtr hWnd, GetWindowEnum uCmd);

        [DllImport(DllName)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport(DllName)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="WindowHandler"/>.
        /// </summary>
        /// <param name="hWnd">The Window Handle.</param>
        public WindowHandler(IntPtr hWnd) { Handle = hWnd; }

        /// <summary>
        /// Gets whether the Window is Visible.
        /// </summary>
        public bool IsVisible => IsWindowVisible(Handle);

        /// <summary>
        /// Gets the Window Handle.
        /// </summary>
        public IntPtr Handle { get; }

        /// <summary>
        /// Gets the Window Title.
        /// </summary>
        public string Title
        {
            get
            {
                var title = new StringBuilder(GetWindowTextLength(Handle) + 1);
                GetWindowText(Handle, title, title.Capacity);
                return title.ToString();
            }
        }

        /// <summary>
        /// Enumerates all Windows.
        /// </summary>
        public static IEnumerable<WindowHandler> Enumerate()
        {
            var list = new List<WindowHandler>();

            EnumWindows((hWnd, lParam) =>
            {
                var wh = new WindowHandler(hWnd);

                list.Add(wh);

                return true;
            }, IntPtr.Zero);

            return list;
        }

        /// <summary>
        /// Enumerates all visible windows with a Title.
        /// </summary>
        public static IEnumerable<WindowHandler> EnumerateVisible()
        {
            foreach (var hWnd in Enumerate().Where(W => W.IsVisible && !string.IsNullOrWhiteSpace(W.Title))
                                            .Select(W => W.Handle))
            {
                if (!User32.GetWindowLong(hWnd, GetWindowLongValue.ExStyle).HasFlag(WindowStyles.AppWindow))
                {
                    if (GetWindow(hWnd, GetWindowEnum.Owner) != IntPtr.Zero)
                        continue;

                    if (User32.GetWindowLong(hWnd, GetWindowLongValue.ExStyle).HasFlag(WindowStyles.ToolWindow))
                        continue;

                    if (User32.GetWindowLong(hWnd, GetWindowLongValue.Style).HasFlag(WindowStyles.Child))
                        continue;
                }

                yield return new WindowHandler(hWnd);
            }
        }
    }
}
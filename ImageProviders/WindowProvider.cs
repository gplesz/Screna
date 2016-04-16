using Screna.Native;
using System;
using System.Drawing;
using System.Windows.Interop;
using SysParams = System.Windows.SystemParameters;

namespace Screna
{
    /// <summary>
    /// Captures the specified window which can change dynamically. 
    /// The captured image is of the size of the whole desktop to accomodate any change in the Window.
    /// </summary>
    public class WindowProvider : ImageProviderBase
    {
        /// <summary>
        /// Height of the Desktop.
        /// </summary>
        public static readonly int DesktopHeight;

        /// <summary>
        /// Width of the Desktop.
        /// </summary>
        public static readonly int DesktopWidth;

        /// <summary>
        /// A <see cref="Rectangle"/> representing the entire Desktop.
        /// </summary>
        public static readonly Rectangle DesktopRectangle;

        /// <summary>
        /// Desktop Handle.
        /// </summary>
        public static readonly IntPtr DesktopHandle = User32.GetDesktopWindow();

        /// <summary>
        /// Taskbar Handle: Shell_TrayWnd.
        /// </summary>
        public static readonly IntPtr TaskbarHandle = User32.FindWindow("Shell_TrayWnd", null);

        static WindowProvider()
        {
            using (var source = new HwndSource(new HwndSourceParameters()))
            {
                var toDevice = source.CompositionTarget.TransformToDevice;

                DesktopHeight = (int)Math.Round(SysParams.VirtualScreenHeight * toDevice.M22);
                DesktopWidth = (int)Math.Round(SysParams.VirtualScreenWidth * toDevice.M11);

                DesktopRectangle = new Rectangle((int)SysParams.VirtualScreenLeft, (int)SysParams.VirtualScreenTop, DesktopWidth, DesktopHeight);
            }
        }

        readonly Func<IntPtr> _hWnd;
        readonly Color _backgroundColor;

        public WindowProvider(IntPtr hWnd = default(IntPtr), Color BackgroundColor = default(Color), params IOverlay[] Overlays)
            : this(() => hWnd, BackgroundColor, Overlays) { }

        public WindowProvider(Func<IntPtr> hWnd, Color BackgroundColor = default(Color), params IOverlay[] Overlays)
            : base(Overlays)
        {
            _hWnd = hWnd;
            _backgroundColor = BackgroundColor;
        }

        /// <summary>
        /// Capture Image.
        /// </summary>
        protected override void OnCapture(Graphics g)
        {
            var windowHandle = _hWnd();

            var rect = DesktopRectangle;

            if (windowHandle != DesktopHandle
                && windowHandle != IntPtr.Zero)
            {
                RECT r;

                if (User32.GetWindowRect(windowHandle, out r))
                    rect = r.ToRectangle();
            }
            
            if (_backgroundColor != Color.Transparent)
                g.FillRectangle(new SolidBrush(_backgroundColor), DesktopRectangle);

            g.CopyFromScreen(rect.Location, 
                             rect.Location,
                             rect.Size,
                             CopyPixelOperation.SourceCopy);
        }

        /// <summary>
        /// Gets the Height of Captured Image = Height of Desktop
        /// </summary>
        public override int Height => DesktopHeight;

        /// <summary>
        /// Gets the Width of Captured Image = Width of Desktop
        /// </summary>
        public override int Width => DesktopWidth;
    }
}

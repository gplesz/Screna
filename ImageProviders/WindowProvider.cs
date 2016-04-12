using Screna.Native;
using System;
using System.Drawing;
using System.Windows.Interop;
using SysParams = System.Windows.SystemParameters;

namespace Screna
{
    public class WindowProvider : IImageProvider
    {
        public static readonly int DesktopHeight, DesktopWidth;

        public static readonly Rectangle DesktopRectangle;

        public static readonly IntPtr DesktopHandle = User32.GetDesktopWindow(),
            TaskbarHandle = User32.FindWindow("Shell_TrayWnd", null);

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
        readonly IOverlay[] _overlays;

        public WindowProvider(IntPtr hWnd = default(IntPtr), Color BackgroundColor = default(Color), params IOverlay[] Overlays)
            : this(() => hWnd, BackgroundColor, Overlays) { }

        public WindowProvider(Func<IntPtr> hWnd, Color BackgroundColor = default(Color), params IOverlay[] Overlays)
        {
            _hWnd = hWnd;
            _overlays = Overlays;
            _backgroundColor = BackgroundColor;
        }

        public Bitmap Capture()
        {
            var windowHandle = _hWnd();

            var rect = DesktopRectangle;

            if (windowHandle != DesktopHandle && windowHandle != IntPtr.Zero)
            {
                RECT r;

                if (User32.GetWindowRect(windowHandle, out r))
                    rect = r.ToRectangle();
            }

            var bmp = new Bitmap(DesktopWidth, DesktopHeight);

            using (var g = Graphics.FromImage(bmp))
            {
                if (_backgroundColor != Color.Transparent)
                    g.FillRectangle(new SolidBrush(_backgroundColor), DesktopRectangle);

                g.CopyFromScreen(rect.Location, 
                                 rect.Location,
                                 rect.Size,
                                 CopyPixelOperation.SourceCopy);

                foreach (var overlay in _overlays)
                    overlay.Draw(g);
            }

            return bmp;
        }

        public int Height => DesktopHeight;

        public int Width => DesktopWidth;

        public void Dispose()
        {
            foreach (var overlay in _overlays)
                overlay.Dispose();
        }
    }
}

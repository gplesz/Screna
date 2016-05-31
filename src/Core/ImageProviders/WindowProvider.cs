using Screna.Native;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
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
        #region PInvoke
        const string DllName = "user32.dll";

        [DllImport(DllName)]
        static extern IntPtr GetDesktopWindow();

        [DllImport(DllName)]
        static extern IntPtr GetForegroundWindow();
        #endregion

        /// <summary>
        /// Height of the Desktop.
        /// </summary>
        public static int DesktopHeight { get; }

        /// <summary>
        /// Width of the Desktop.
        /// </summary>
        public static int DesktopWidth { get; }

        /// <summary>
        /// A <see cref="Rectangle"/> representing the entire Desktop.
        /// </summary>
        public static Rectangle DesktopRectangle { get; }

        /// <summary>
        /// Desktop Handle.
        /// </summary>
        public static IntPtr DesktopHandle { get; } = GetDesktopWindow();

        /// <summary>
        /// Gets the Foreground Window Handle.
        /// </summary>
        public static IntPtr ForegroundWindowHandle => GetForegroundWindow();

        /// <summary>
        /// Taskbar Handle: Shell_TrayWnd.
        /// </summary>
        public static IntPtr TaskbarHandle { get; } = User32.FindWindow("Shell_TrayWnd", null);

        static WindowProvider()
        {
            using (var source = new HwndSource(new HwndSourceParameters()))
            {
                var toDevice = source.CompositionTarget?.TransformToDevice;

                DesktopHeight = toDevice == null ? (int)SysParams.VirtualScreenHeight : (int)Math.Round(SysParams.VirtualScreenHeight * toDevice.Value.M22);
                DesktopWidth = toDevice == null ? (int)SysParams.VirtualScreenWidth : (int)Math.Round(SysParams.VirtualScreenWidth * toDevice.Value.M11);

                DesktopRectangle = new Rectangle((int)SysParams.VirtualScreenLeft, (int)SysParams.VirtualScreenTop, DesktopWidth, DesktopHeight);
            }
        }

        readonly Func<IntPtr> _hWnd;
        readonly Color _backgroundColor;

        /// <summary>
        /// Creates a new instance of <see cref="WindowProvider"/>.
        /// </summary>
        /// <param name="Handle">Handle of the Window to Capture.</param>
        /// <param name="BackgroundColor"><see cref="Color"/> to fill blank background.</param>
        /// <param name="Overlays">Overlays to draw.</param>
        public WindowProvider(IntPtr Handle = default(IntPtr), Color BackgroundColor = default(Color), params IOverlay[] Overlays)
            : this(() => Handle, BackgroundColor, Overlays) { }

        /// <summary>
        /// Creates a new instance of <see cref="WindowProvider"/>.
        /// </summary>
        /// <param name="HandleFunc">A Function returning the Handle of the Window to Capture.</param>
        /// <param name="BackgroundColor"><see cref="Color"/> to fill blank background.</param>
        /// <param name="Overlays">Overlays to draw.</param>
        /// <exception cref="ArgumentNullException"><paramref name="HandleFunc"/> is null.</exception>
        public WindowProvider(Func<IntPtr> HandleFunc, Color BackgroundColor = default(Color), params IOverlay[] Overlays)
            : base(Overlays)
        {
            if (HandleFunc == null)
                throw new ArgumentNullException(nameof(HandleFunc));

            _hWnd = HandleFunc;
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
        /// Gets the Height of Captured Image = Height of Desktop.
        /// </summary>
        public override int Height => DesktopHeight;

        /// <summary>
        /// Gets the Width of Captured Image = Width of Desktop.
        /// </summary>
        public override int Width => DesktopWidth;
    }
}

using Screna.Native;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Screna
{
    /// <summary>
    /// Draws the MouseCursor on an Image
    /// </summary>
    public class MouseCursor : IOverlay
    {
        const int CursorShowing = 1;

        IconInfo _icInfo;
        IntPtr _hIcon;
        CursorInfo _cursorInfo;

        /// <summary>
        /// Create a new instance of <see cref="MouseCursor"/>.
        /// </summary>
        /// <param name="Include">Whether to include Mouse Cursor. Setting this to false bypasses this Overlay.</param>
        public MouseCursor(bool Include = true) { this.Include = Include; }

        /// <summary>
        /// Gets the Current Mouse Cursor Position.
        /// </summary>
        public static Point CursorPosition
        {
            get
            {
                var p = new Point();
                User32.GetCursorPos(ref p);
                return p;
            }
        }

        /// <summary>
        /// Gets or Sets whether to include Mouse Cursor. Setting this to false bypasses this Overlay.
        /// </summary>
        public bool Include { get; set; }

        /// <summary>
        /// Draws this overlay.
        /// </summary>
        /// <param name="g">A <see cref="Graphics"/> object to draw upon.</param>
        /// <param name="Offset">Offset from Origin of the Captured Area.</param>
        public void Draw(Graphics g, Point Offset = default(Point))
        {
            if (!Include)
                return;

            _cursorInfo = new CursorInfo { cbSize = Marshal.SizeOf(typeof(CursorInfo)) };

            if (!User32.GetCursorInfo(out _cursorInfo))
                return;

            if (_cursorInfo.flags != CursorShowing)
                return;

            _hIcon = User32.CopyIcon(_cursorInfo.hCursor);

            if (!User32.GetIconInfo(_hIcon, out _icInfo))
                return;

            var location = new Point(_cursorInfo.ptScreenPos.X - Offset.X - _icInfo.xHotspot,
                _cursorInfo.ptScreenPos.Y - Offset.Y - _icInfo.yHotspot);

            if (_hIcon != IntPtr.Zero)
                using (var cursorBmp = Icon.FromHandle(_hIcon).ToBitmap())
                    g.DrawImage(cursorBmp, new Rectangle(location, cursorBmp.Size));

            User32.DestroyIcon(_hIcon);
        }

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public void Dispose() { }
    }
}
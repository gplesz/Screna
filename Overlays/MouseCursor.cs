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

        public MouseCursor(bool Include = true) { this.Include = Include; }

        public static Point CursorPosition
        {
            get
            {
                var p = new Point();
                User32.GetCursorPos(ref p);
                return p;
            }
        }

        public bool Include { get; set; }

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

        public void Dispose() { }
    }
}
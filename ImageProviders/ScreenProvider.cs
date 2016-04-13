using System.Drawing;
using System.Windows.Forms;

namespace Screna
{
    /// <summary>
    /// Capture a Specific Screen.
    /// </summary>
    public class ScreenProvider : IImageProvider
    {
        readonly Screen _screen;
        readonly IOverlay[] _overlays;

        /// <summary>
        /// Creates a new instance of <see cref="ScreenProvider"/>.
        /// </summary>
        /// <param name="Screen">The Screen to Capture.</param>
        /// <param name="Overlays">Items to Overlay on Captured images.</param>
        public ScreenProvider(Screen Screen, params IOverlay[] Overlays)
        {
            _screen = Screen;
            _overlays = Overlays;
        }

        /// <summary>
        /// Capture Frame.
        /// </summary>
        public Bitmap Capture()
        {
            var bmp = ScreenShot.Capture(_screen);

            using (var g = Graphics.FromImage(bmp))
                foreach (var overlay in _overlays)
                    overlay.Draw(g, Rectangle.Location);

            return bmp;
        }

        /// <summary>
        /// Height of the Screen.
        /// </summary>
        public int Height => _screen.Bounds.Height;

        /// <summary>
        /// Width of the Screen.
        /// </summary>
        public int Width => _screen.Bounds.Height;
        
        Rectangle Rectangle => _screen.Bounds;

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public void Dispose()
        {
            foreach (var overlay in _overlays)
                overlay.Dispose();
        }
    }
}

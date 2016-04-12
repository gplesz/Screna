using System.Drawing;
using System.Windows.Forms;

namespace Screna
{
    public class ScreenProvider : IImageProvider
    {
        readonly Screen _screen;
        readonly IOverlay[] _overlays;

        public ScreenProvider(Screen Screen, params IOverlay[] Overlays)
        {
            _screen = Screen;
            _overlays = Overlays;
        }

        public Bitmap Capture()
        {
            var bmp = ScreenShot.Capture(_screen);

            using (var g = Graphics.FromImage(bmp))
                foreach (var overlay in _overlays)
                    overlay.Draw(g, Rectangle.Location);

            return bmp;
        }

        public int Height => _screen.Bounds.Height;

        public int Width => _screen.Bounds.Height;
        
        Rectangle Rectangle => _screen.Bounds;

        public void Dispose()
        {
            foreach (var overlay in _overlays)
                overlay.Dispose();
        }
    }
}

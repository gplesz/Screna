using System.Drawing;

namespace Screna
{
    public class RegionProvider : IImageProvider
    {
        readonly Rectangle _region;
        readonly IOverlay[] _overlays;
        
        public RegionProvider(Rectangle Region, params IOverlay[] Overlays)
        {
            _region = Region;
            _overlays = Overlays;
        }

        public Bitmap Capture()
        {
            var bmp = new Bitmap(_region.Width, _region.Height);

            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(_region.Location,
                                 Point.Empty,
                                 _region.Size,
                                 CopyPixelOperation.SourceCopy);

                foreach (var overlay in _overlays)
                    overlay.Draw(g, _region.Location);
            }

            return bmp;
        }

        public int Height => _region.Height;

        public int Width => _region.Width;

        public void Dispose()
        {
            foreach (var overlay in _overlays)
                overlay.Dispose();
        }
    }
}

using System.Drawing;

namespace Screna
{
    /// <summary>
    /// Captures the Region specified by a Rectangle.
    /// </summary>
    public class RegionProvider : IImageProvider
    {
        readonly Rectangle _region;
        readonly IOverlay[] _overlays;
        
        /// <summary>
        /// Creates a new instance of <see cref="RegionProvider"/>.
        /// </summary>
        /// <param name="Region">Region to Capture.</param>
        /// <param name="Overlays">Any Overlays to draw.</param>
        public RegionProvider(Rectangle Region, params IOverlay[] Overlays)
        {
            _region = Region;
            _overlays = Overlays;
        }

        /// <summary>
        /// Capture an image.
        /// </summary>
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

        /// <summary>
        /// Height of Captured image.
        /// </summary>
        public int Height => _region.Height;

        /// <summary>
        /// Width of Captured image.
        /// </summary>
        public int Width => _region.Width;

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

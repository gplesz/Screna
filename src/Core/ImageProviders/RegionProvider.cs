using System.Drawing;

namespace Screna
{
    /// <summary>
    /// Captures the Region specified by a Rectangle.
    /// </summary>
    public class RegionProvider : ImageProviderBase
    {
        readonly Rectangle _region;
        
        /// <summary>
        /// Creates a new instance of <see cref="RegionProvider"/>.
        /// </summary>
        /// <param name="Region">Region to Capture.</param>
        /// <param name="Overlays">Any Overlays to draw.</param>
        public RegionProvider(Rectangle Region, params IOverlay[] Overlays)
            : base(Overlays, Region.Location)
        {
            _region = Region;
        }

        /// <summary>
        /// Capture an image.
        /// </summary>
        protected override void OnCapture(Graphics g)
        {
            g.CopyFromScreen(_region.Location,
                             Point.Empty,
                             _region.Size,
                             CopyPixelOperation.SourceCopy);
        }

        /// <summary>
        /// Height of Captured image.
        /// </summary>
        public override int Height => _region.Height;

        /// <summary>
        /// Width of Captured image.
        /// </summary>
        public override int Width => _region.Width;
    }
}

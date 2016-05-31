using System.Drawing;

namespace Screna
{
    /// <summary>
    /// An abstract implementation of <see cref="IImageProvider"/> interface.
    /// </summary>
    public abstract class ImageProviderBase : IImageProvider
    {
        readonly IOverlay[] _overlays;
        readonly Point _offset;

        /// <summary>
        /// Constructor for <see cref="ImageProviderBase"/>.
        /// </summary>
        /// <param name="Overlays">Array of <see cref="IOverlay"/>(s) to apply.</param>
        /// <param name="Offset">Offset for drawing overlays.</param>
        protected ImageProviderBase(IOverlay[] Overlays, Point Offset = default(Point))
        {
            _overlays = Overlays;
            _offset = Offset;
        }

        /// <summary>
        /// Captures an Image.
        /// </summary>
        public Bitmap Capture()
        {
            var bmp = new Bitmap(Width, Height);

            using (var g = Graphics.FromImage(bmp))
            {
                OnCapture(g);

                if (_overlays != null)
                    foreach (var overlay in _overlays)
                        overlay?.Draw(g, _offset);
            }

            return bmp;
        }

        /// <summary>
        /// Implemented by derived classes for the actual capture process.
        /// </summary>
        protected abstract void OnCapture(Graphics g);

        /// <summary>
        /// Height of Captured image.
        /// </summary>
        public abstract int Height { get; }

        /// <summary>
        /// Width of Captured image.
        /// </summary>
        public abstract int Width { get; }

        /// <summary>
        /// Frees all resources used by this instance.
        /// </summary>
        public virtual void Dispose()
        {
            if (_overlays == null)
                return;

            foreach (var overlay in _overlays)
                overlay?.Dispose();
        }
    }
}

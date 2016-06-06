using System.Drawing;

namespace Screna
{
    /// <summary>
    /// An abstract implementation of <see cref="IImageProvider"/> interface.
    /// </summary>
    public abstract class ImageProviderBase : IImageProvider
    {
        readonly IOverlay[] _overlays;
        readonly Rectangle _rectangle;

        /// <summary>
        /// Constructor for <see cref="ImageProviderBase"/>.
        /// </summary>
        /// <param name="Overlays">Array of <see cref="IOverlay"/>(s) to apply.</param>
        /// <param name="Rectangle">A <see cref="Rectangle"/> representing the captured region.</param>
        protected ImageProviderBase(IOverlay[] Overlays, Rectangle Rectangle)
        {
            _overlays = Overlays;
            _rectangle = Rectangle;

            Width = Rectangle.Width;
            Height = Rectangle.Height;
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
                        overlay?.Draw(g, _rectangle.Location);
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
        public int Height { get; }

        /// <summary>
        /// Width of Captured image.
        /// </summary>
        public int Width { get; }

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

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Screna
{
    /// <summary>
    /// Capture a Specific Screen.
    /// </summary>
    public class ScreenProvider : ImageProviderBase
    {
        readonly Screen _screen;

        /// <summary>
        /// Creates a new instance of <see cref="ScreenProvider"/>.
        /// </summary>
        /// <param name="Screen">The Screen to Capture.</param>
        /// <param name="Overlays">Items to Overlay on Captured images.</param>
        /// <exception cref="ArgumentNullException"><paramref name="Screen"/> is null.</exception>
        public ScreenProvider(Screen Screen, params IOverlay[] Overlays)
            : base(Overlays, Screen?.Bounds.Location ?? Point.Empty)
        {
            if (Screen == null)
                throw new ArgumentNullException(nameof(Screen));

            _screen = Screen;
        }

        /// <summary>
        /// Capture Frame.
        /// </summary>
        protected override void OnCapture(Graphics g) => g.DrawImage(ScreenShot.Capture(_screen), Point.Empty);
        
        /// <summary>
        /// Height of the Screen.
        /// </summary>
        public override int Height => _screen.Bounds.Height;

        /// <summary>
        /// Width of the Screen.
        /// </summary>
        public override int Width => _screen.Bounds.Height;
    }
}

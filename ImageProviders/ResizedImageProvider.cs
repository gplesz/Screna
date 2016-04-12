using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Screna
{
    public class ResizedImageProvider : IImageProvider
    {
        readonly float _resizeWidth, _resizeHeight;

        readonly IImageProvider _imageSource;
        readonly Color _backgroundColor;

        public ResizedImageProvider(IImageProvider ImageSource, int TargetWidth, int TargetHeight, Color BackgroundColor)
        {
            _imageSource = ImageSource;
            _backgroundColor = BackgroundColor;

            Height = TargetHeight;
            Width = TargetWidth;

            int originalWidth = ImageSource.Width,
                originalHeight = ImageSource.Height;

            var ratio = Math.Min((float)TargetWidth / originalWidth, (float)TargetHeight / originalHeight);

            _resizeWidth = originalWidth * ratio;
            _resizeHeight = originalHeight * ratio;
        }

        public Bitmap Capture()
        {
            var bmp = _imageSource.Capture();

            var resizedBmp = new Bitmap(Width, Height);

            using (var g = Graphics.FromImage(resizedBmp))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;

                if (_backgroundColor != Color.Transparent)
                    g.FillRectangle(new SolidBrush(_backgroundColor), 0, 0, Width, Height);

                g.DrawImage(bmp, 0, 0, _resizeWidth, _resizeHeight);
            }

            return resizedBmp;
        }

        public int Height { get; }

        public int Width { get; }

        public void Dispose() { }
    }
}
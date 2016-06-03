using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Screna.Tests
{
    [TestClass]
    public class ImageProviders
    {
        [TestMethod]
        public void ResizedImageProvider()
        {
            const int targetWidth = 6,
                targetHeight = 7;
            
            var resizedImageProvider = new ResizedImageProvider(SetUp.Get<IImageProvider>(), targetWidth, targetHeight, Color.Empty);

            var capture = resizedImageProvider.Capture();

            Assert.AreEqual(targetWidth, capture.Width);
            Assert.AreEqual(targetWidth, resizedImageProvider.Width);

            Assert.AreEqual(targetHeight, capture.Height);
            Assert.AreEqual(targetHeight, resizedImageProvider.Height);
        }

        [TestMethod]
        public void RotatedImageProvider()
        {
            var sourceImageProvider = SetUp.Get<IImageProvider>();

            var rotatedImageProvider = new RotatedImageProvider(sourceImageProvider, RotateFlipType.Rotate90FlipNone);

            var capture = rotatedImageProvider.Capture();
            
            Assert.AreEqual(sourceImageProvider.Width, capture.Height);
            Assert.AreEqual(sourceImageProvider.Width, rotatedImageProvider.Height);

            Assert.AreEqual(sourceImageProvider.Height, capture.Width);
            Assert.AreEqual(sourceImageProvider.Height, rotatedImageProvider.Width);
        }

        [TestMethod]
        public void RotatedImageProviderFlip()
        {
            var sourceImageProvider = SetUp.Get<IImageProvider>();

            var rotatedImageProvider = new RotatedImageProvider(sourceImageProvider, RotateFlipType.Rotate180FlipNone);

            var capture = rotatedImageProvider.Capture();

            Assert.AreEqual(sourceImageProvider.Width, capture.Width);
            Assert.AreEqual(sourceImageProvider.Width, rotatedImageProvider.Width);

            Assert.AreEqual(sourceImageProvider.Height, capture.Height);
            Assert.AreEqual(sourceImageProvider.Height, rotatedImageProvider.Height);
        }
    }
}
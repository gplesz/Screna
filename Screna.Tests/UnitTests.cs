using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Screna.Tests
{
    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void RecorderStates()
        {
            var videoFileWriter = new Mock<IVideoFileWriter>();

            var imageProvider = new Mock<IImageProvider>();
            
            var recorder = new Recorder(videoFileWriter.Object, imageProvider.Object, 10);

            Assert.AreEqual(recorder.State, RecorderState.Ready);
            
            recorder.Start();

            Assert.AreEqual(recorder.State, RecorderState.Recording);

            recorder.Pause();

            Assert.AreEqual(recorder.State, RecorderState.Paused);

            recorder.Start();

            Assert.AreEqual(recorder.State, RecorderState.Recording);

            recorder.Stop();

            Assert.AreEqual(recorder.State, RecorderState.Stopped);
        }

        [TestMethod]
        public void ResizedImageProvider()
        {
            const int originalWidth = 30,
                originalHeight = 40,
                targetWidth = 100,
                targetHeight = 200;

            var imageProvider = new Mock<IImageProvider>();
            imageProvider.Setup(x => x.Capture()).Returns(new Bitmap(originalWidth, originalHeight));
            imageProvider.Setup(x => x.Width).Returns(originalWidth);
            imageProvider.Setup(x => x.Height).Returns(originalHeight);
            
            var resizedImageProvider = new ResizedImageProvider(imageProvider.Object, targetWidth, targetHeight, Color.Empty);

            var capture = resizedImageProvider.Capture();

            Assert.AreEqual(targetWidth, capture.Width);
            Assert.AreEqual(targetHeight, capture.Height);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Screna.Tests
{
    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void RecorderStates()
        {
            var recorder = new Recorder(SetUp.Get<IVideoFileWriter>(), SetUp.Get<IImageProvider>(), 10);

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
    }
}

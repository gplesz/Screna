using System;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Screna.Audio;

namespace Screna.Tests
{
    [TestClass]
    public class ArgumentNull
    {
        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void AudioRecorderNullProvider()
        {
            new AudioRecorder(null, SetUp.Get<IAudioFileWriter>());
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void AudioRecorderNullWriter()
        {
            new AudioRecorder(SetUp.Get<IAudioProvider>(), null);
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void ResizedImageProviderNullSource()
        {
            using (new ResizedImageProvider(null, 100, 100, Color.AliceBlue)) { }
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void ScreenProviderNullSource()
        {
            using (new ScreenProvider(null)) { }
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void WindowProviderNullFunc()
        {
            using (new WindowProvider(null)) { }
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void RecorderVideoFileWriterNull()
        {
            new Recorder(null, SetUp.Get<IImageProvider>(), 10);
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void RecorderImageProviderNull()
        {
            new Recorder(SetUp.Get<IVideoFileWriter>(), null, 10);
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void UnconstrainedGifEncoderNull()
        {
            new UnconstrainedFrameRateGifRecorder(null, SetUp.Get<IImageProvider>());
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void UnconstrainedGifSourceNull()
        {
            new UnconstrainedFrameRateGifRecorder(new GifWriter(Stream.Null), null);
        }
    }
}

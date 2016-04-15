using System;
using System.Threading;
using System.Threading.Tasks;

namespace Screna
{
    /// <summary>
    /// An <see cref="IRecorder"/> which records to a Gif using Delay for each frame instead of Frame Rate.
    /// </summary>
    public class UnconstrainedFrameRateGifRecorder : IRecorder
    {
        #region Fields
        GifWriter _videoEncoder;
        IImageProvider _imageProvider;

        Thread _recordThread;

        ManualResetEvent _stopCapturing = new ManualResetEvent(false),
            _continueCapturing = new ManualResetEvent(false);
        #endregion
        
        /// <summary>
        /// Stops Recording.
        /// </summary>
        ~UnconstrainedFrameRateGifRecorder() { Stop(); }

        /// <summary>
        /// Creates a new instance of <see cref="UnconstrainedFrameRateGifRecorder"/>.
        /// </summary>
        /// <param name="Encoder">The <see cref="GifWriter"/> to write into.</param>
        /// <param name="ImageProvider">The <see cref="IImageProvider"/> providing the individual frames.</param>
        public UnconstrainedFrameRateGifRecorder(GifWriter Encoder, IImageProvider ImageProvider)
        {
            // Init Fields
            _imageProvider = ImageProvider;
            _videoEncoder = Encoder;

            // GifWriter.Init not needed.

            // RecordThread Init
            if (ImageProvider != null)
                _recordThread = new Thread(Record)
                {
                    Name = "Captura.Record",
                    IsBackground = true
                };


            // Not Actually Started, Waits for _continueCapturing to be Set
            _recordThread?.Start();
        }

        /// <summary>
        /// Fired when Recording Stops.
        /// </summary>
        public event Action<Exception> RecordingStopped;

        /// <summary>
        /// Start Recording.
        /// </summary>
        /// <param name="Delay">Delay before recording starts.</param>
        public void Start(int Delay = 0)
        {
            new Thread(e =>
            {
                try
                {
                    Thread.Sleep((int)e);

                    if (_recordThread != null)
                        _continueCapturing.Set();
                }
                catch (Exception E) { RecordingStopped?.Invoke(E); }
            }).Start(Delay);
        }

        /// <summary>
        /// Stop Recording and Perform Cleanup.
        /// </summary>
        public void Stop()
        {
            // Resume if Paused
            _continueCapturing?.Set();

            // Video
            if (_recordThread != null)
            {
                if (_stopCapturing != null && !_stopCapturing.SafeWaitHandle.IsClosed)
                    _stopCapturing.Set();

                if (!_recordThread.Join(500)) 
                    _recordThread.Abort();

                _recordThread = null;
            }

            if (_imageProvider != null)
            {
                _imageProvider.Dispose();
                _imageProvider = null;
            }

            // WaitHandles
            if (_stopCapturing != null && !_stopCapturing.SafeWaitHandle.IsClosed)
            {
                _stopCapturing.Dispose();
                _stopCapturing = null;
            }

            if (_continueCapturing != null && !_continueCapturing.SafeWaitHandle.IsClosed)
            {
                _continueCapturing.Dispose();
                _continueCapturing = null;
            }

            // Writers
            if (_videoEncoder == null)
                return;

            _videoEncoder.Dispose();
            _videoEncoder = null;
        }

        /// <summary>
        /// Pause Recording.
        /// </summary>
        public void Pause() { if (_recordThread != null) _continueCapturing.Reset(); }

        void Record()
        {
            try
            {
                var lastFrameWriteTime = DateTime.MinValue;
                Task lastFrameWriteTask = null;

                while (!_stopCapturing.WaitOne(0) && _continueCapturing.WaitOne())
                {
                    var frame = _imageProvider.Capture();

                    var delay = lastFrameWriteTime == DateTime.MinValue ? 0
                        : (int)(DateTime.Now - lastFrameWriteTime).TotalMilliseconds;

                    lastFrameWriteTime = DateTime.Now;

                    lastFrameWriteTask = _videoEncoder.WriteFrameAsync(frame, delay);
                }

                // Wait for the last frame is written
                lastFrameWriteTask?.Wait();
            }
            catch (Exception E)
            {
                Stop();
                RecordingStopped?.Invoke(E);
            }
        }
    }
}

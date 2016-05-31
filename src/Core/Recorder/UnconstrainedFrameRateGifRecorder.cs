using System;
using System.Threading;
using System.Threading.Tasks;

namespace Screna
{
    /// <summary>
    /// An <see cref="IRecorder"/> which records to a Gif using Delay for each frame instead of Frame Rate.
    /// </summary>
    public class UnconstrainedFrameRateGifRecorder : RecorderBase
    {
        #region Fields
        GifWriter _videoEncoder;
        IImageProvider _imageProvider;

        Thread _recordThread;

        ManualResetEvent _stopCapturing = new ManualResetEvent(false),
            _continueCapturing = new ManualResetEvent(false);
        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="UnconstrainedFrameRateGifRecorder"/>.
        /// </summary>
        /// <param name="Encoder">The <see cref="GifWriter"/> to write into.</param>
        /// <param name="ImageProvider">The <see cref="IImageProvider"/> providing the individual frames.</param>
        /// <exception cref="ArgumentNullException"><paramref name="Encoder"/> or <paramref name="ImageProvider"/> is null.</exception>
        public UnconstrainedFrameRateGifRecorder(GifWriter Encoder, IImageProvider ImageProvider)
        {
            if (Encoder == null)
                throw new ArgumentNullException(nameof(Encoder));

            if (ImageProvider == null)
                throw new ArgumentNullException(nameof(ImageProvider));

            // Init Fields
            _imageProvider = ImageProvider;
            _videoEncoder = Encoder;
            
            // GifWriter.Init not needed.

            // RecordThread Init
            _recordThread = new Thread(Record)
            {
                Name = "Captura.Record",
                IsBackground = true
            };


            // Not Actually Started, Waits for _continueCapturing to be Set
            _recordThread?.Start();
        }

        /// <summary>
        /// Override this method with the code to start recording.
        /// </summary>
        protected override void OnStart() => _continueCapturing.Set();

        /// <summary>
        /// Override this method with the code to stop recording.
        /// </summary>
        protected override void OnStop()
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
        /// Override this method with the code to pause recording.
        /// </summary>
        protected override void OnPause() => _continueCapturing.Reset();

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
            catch (Exception e)
            {
                Stop();
                RaiseRecordingStopped(e);
            }
        }
    }
}

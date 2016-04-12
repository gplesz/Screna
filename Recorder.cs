// Adapted from SharpAvi Screencast Sample by Vasilli Masillov
using Screna.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Screna
{
    public class Recorder : IRecorder
    {
        #region Fields
        IAudioProvider _audioProvider;
        IVideoFileWriter _videoEncoder;
        IImageProvider _imageProvider;

        Thread _recordThread;

        ManualResetEvent _stopCapturing = new ManualResetEvent(false),
            _continueCapturing = new ManualResetEvent(false);

        readonly AutoResetEvent _videoFrameWritten = new AutoResetEvent(false),
            _audioBlockWritten = new AutoResetEvent(false);

        readonly SynchronizationContext _syncContext;
        #endregion

        ~Recorder() { Stop(); }

        public event Action<Exception> RecordingStopped;

        void RaiseRecordingStopped(Exception E)
        {
            var handler = RecordingStopped;

            if (handler == null)
                return;

            if (_syncContext != null)
                _syncContext.Post(S => handler(E), null);

            else handler(E);
        }

        public Recorder(IVideoFileWriter Encoder, IImageProvider ImageProvider, IAudioProvider AudioProvider = null)
        {
            // Init Fields
            _imageProvider = ImageProvider;
            _videoEncoder = Encoder;
            _audioProvider = AudioProvider;

            _syncContext = SynchronizationContext.Current;

            // Audio Init
            if (_videoEncoder.RecordsAudio
                && AudioProvider != null)
                AudioProvider.DataAvailable += AudioDataAvailable;
            else _audioProvider = null;

            // RecordThread Init
            if (ImageProvider != null)
                _recordThread = new Thread(Record)
                {
                    Name = "Captura.Record",
                    IsBackground = true
                };

            // Not Actually Started, Waits for ContinueThread to be Set
            _recordThread?.Start();
        }

        public void Start(int Delay = 0)
        {
            new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(Delay);

                        if (_recordThread != null)
                            _continueCapturing.Set();

                        if (_audioProvider == null)
                            return;

                        _videoFrameWritten.Set();
                        _audioBlockWritten.Reset();

                        _audioProvider.Start();
                    }
                    catch (Exception e) { RaiseRecordingStopped(e); }
                }).Start();
        }

        public void Pause()
        {
            if (_recordThread != null)
                _continueCapturing.Reset();

            _audioProvider?.Stop();
        }

        public void Stop()
        {
            // Resume if Paused
            _continueCapturing?.Set();

            // Video
            if (_recordThread != null)
            {
                if (_stopCapturing != null
                    && !_stopCapturing.SafeWaitHandle.IsClosed)
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

            // Audio Source
            if (_audioProvider != null)
            {
                _audioProvider.Dispose();
                _audioProvider = null;
            }

            // WaitHandles
            if (_stopCapturing != null
                && !_stopCapturing.SafeWaitHandle.IsClosed)
            {
                _stopCapturing.Dispose();
                _stopCapturing = null;
            }

            if (_continueCapturing != null
                && !_continueCapturing.SafeWaitHandle.IsClosed)
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

        void Record()
        {
            try
            {
                var frameInterval = TimeSpan.FromSeconds(1 / (double)_videoEncoder.FrameRate);
                Task frameWriteTask = null;
                var timeTillNextFrame = TimeSpan.Zero;

                while (!_stopCapturing.WaitOne(timeTillNextFrame)
                    && _continueCapturing.WaitOne())
                {
                    var timestamp = DateTime.Now;

                    var frame = _imageProvider.Capture();

                    // Wait for the previous frame is written
                    if (frameWriteTask != null)
                    {
                        frameWriteTask.Wait();
                        _videoFrameWritten.Set();
                    }

                    if (_audioProvider != null
                        && _audioProvider.IsSynchronizable)
                        if (WaitHandle.WaitAny(new WaitHandle[] { _audioBlockWritten, _stopCapturing }) == 1)
                            break;

                    // Start asynchronous (encoding and) writing of the new frame
                    frameWriteTask = _videoEncoder.WriteFrameAsync(frame);

                    timeTillNextFrame = timestamp + frameInterval - DateTime.Now;
                    if (timeTillNextFrame < TimeSpan.Zero)
                        timeTillNextFrame = TimeSpan.Zero;
                }

                // Wait for the last frame is written
                frameWriteTask?.Wait();
            }
            catch (Exception e)
            {
                Stop();

                RaiseRecordingStopped(e);
            }
        }

        void AudioDataAvailable(byte[] Buffer, int BytesRecorded)
        {
            if (_audioProvider.IsSynchronizable)
            {
                if (WaitHandle.WaitAny(new WaitHandle[] {_videoFrameWritten, _stopCapturing}) != 0)
                    return;

                _videoEncoder.WriteAudio(Buffer, BytesRecorded);

                _audioBlockWritten.Set();
            }
            else _videoEncoder.WriteAudio(Buffer, BytesRecorded);
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Screna.Audio
{
    enum PlaybackState { Stopped, Playing, Paused }

    class WasapiSilenceOut
    {
        AudioClient _audioClient;
        AudioRenderClient _renderClient;
        readonly int _latencyMilliseconds;
        int _bufferFrameCount, _bytesPerFrame;
        readonly EventWaitHandle _frameEventWaitHandle;
        volatile PlaybackState _playbackState;
        Thread _playThread;
        readonly WaveFormat _outputFormat;
        readonly SynchronizationContext _syncContext;

        public event Action<Exception> PlaybackStopped;

        public WasapiSilenceOut(WasapiAudioDevice Device, int Latency)
        {
            _audioClient = Device.AudioClient;
            _latencyMilliseconds = Latency;
            _syncContext = SynchronizationContext.Current;
            _outputFormat = _audioClient.MixFormat; // allow the user to query the default format for shared mode streams

            long latencyRefTimes = _latencyMilliseconds * 10000;

            const int eventCallback = 0x00040000;

            // With EventCallBack and Shared, both latencies must be set to 0 (update - not sure this is true anymore)
            _audioClient.Initialize(AudioClientShareMode.Shared, eventCallback, latencyRefTimes, 0, _outputFormat, Guid.Empty);

            // Windows 10 returns 0 from stream latency, resulting in maxing out CPU usage later
            var streamLatency = _audioClient.StreamLatency;
            if (streamLatency != 0)
                // Get back the effective latency from AudioClient
                _latencyMilliseconds = (int)(streamLatency / 10000);

            // Create the Wait Event Handle
            _frameEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            _audioClient.SetEventHandle(_frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());

            // Get the RenderClient
            _renderClient = _audioClient.AudioRenderClient;
        }

        void PlayThread()
        {
            Exception e = null;
            try
            {
                // fill a whole buffer
                _bufferFrameCount = _audioClient.BufferSize;
                _bytesPerFrame = _outputFormat.Channels * _outputFormat.BitsPerSample / 8;
                FillBuffer(_bufferFrameCount);
                
                _audioClient.Start();

                while (_playbackState != PlaybackState.Stopped)
                {
                    // If still playing and notification is ok
                    if (!_frameEventWaitHandle.WaitOne(3*_latencyMilliseconds) || _playbackState != PlaybackState.Playing)
                        continue;

                    // See how much buffer space is available.
                    var numFramesAvailable = _bufferFrameCount - _audioClient.CurrentPadding;

                    if (numFramesAvailable > 10)
                        FillBuffer(numFramesAvailable);
                }

                Thread.Sleep(_latencyMilliseconds / 2);
                _audioClient.Stop();

                if (_playbackState == PlaybackState.Stopped) _audioClient.Reset();
            }
            catch (Exception ex) { e = ex; }
            finally
            {
                var handler = PlaybackStopped;
                if (handler != null)
                {
                    if (_syncContext == null) handler(e);
                    else _syncContext.Post(State => handler(e), null);
                }
            }
        }

        void FillBuffer(int FrameCount)
        {
            var buffer = _renderClient.GetBuffer(FrameCount);
            var readLength = FrameCount * _bytesPerFrame;

            for (var i = 0; i < readLength; ++i)
                Marshal.WriteByte(buffer, i, 0);

            _renderClient.ReleaseBuffer(FrameCount);
        }

        public void Play()
        {
            switch (_playbackState)
            {
                case PlaybackState.Playing:
                    return;

                case PlaybackState.Stopped:
                    _playThread = new Thread(PlayThread);
                    _playbackState = PlaybackState.Playing;
                    _playThread.Start();
                    break;

                default:
                    _playbackState = PlaybackState.Playing;
                    break;
            }
        }

        public void Stop()
        {
            if (_playbackState == PlaybackState.Stopped)
                return;

            _playbackState = PlaybackState.Stopped;
            _playThread.Join();
            _playThread = null;
        }

        public void Pause()
        {
            if (_playbackState == PlaybackState.Playing)
                _playbackState = PlaybackState.Paused;
        }

        public void Dispose()
        {
            if (_audioClient == null)
                return;

            Stop();

            _audioClient.Dispose();
            _audioClient = null;
            _renderClient = null;
        }
    }
}

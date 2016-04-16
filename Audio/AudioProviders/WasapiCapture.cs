using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Screna.Audio
{
    /// <summary>
    /// Represents state of a capture device
    /// </summary>
    public enum CaptureState
    {
        /// <summary>
        /// Not recording
        /// </summary>
        Stopped,
        /// <summary>
        /// Beginning to record
        /// </summary>
        Starting,
        /// <summary>
        /// Recording in progress
        /// </summary>
        Capturing,
        /// <summary>
        /// Requesting stop
        /// </summary>
        Stopping
    }

    /// <summary>
    /// Audio Capture using Wasapi.
    /// </summary>
    public class WasapiCapture : IAudioProvider
    {
        const long ReftimesPerSec = 10000000,
            ReftimesPerMillisec = 10000;
        volatile CaptureState _captureState;
        byte[] _recordBuffer;
        Thread _captureThread;
        AudioClient _audioClient;
        int _bytesPerFrame;
        WaveFormat _waveFormat;
        bool _initialized;
        readonly SynchronizationContext _syncContext;
        readonly int _audioBufferMillisecondsLength;

        /// <summary>
        /// Indicates recorded data is available 
        /// </summary>
        public event Action<byte[], int> DataAvailable;

        /// <summary>
        /// Indicates that all recorded data has now been received.
        /// </summary>
        public event Action<Exception> RecordingStopped;

        /// <summary>
        /// Creates a new instances of <see cref="WasapiCapture"/> class using <see cref="DefaultDevice"/>.
        /// </summary>
        public WasapiCapture() : this(DefaultDevice) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="WasapiCapture" /> class.
        /// </summary>
        /// <param name="CaptureDevice">The capture device.</param>
        /// <param name="AudioBufferMillisecondsLength">Length of the audio buffer in milliseconds. A lower value means lower latency but increased CPU usage.</param>
        public WasapiCapture(WasapiAudioDevice CaptureDevice, int AudioBufferMillisecondsLength = 100)
        {
            _syncContext = SynchronizationContext.Current;
            _audioClient = CaptureDevice.AudioClient;
            _audioBufferMillisecondsLength = AudioBufferMillisecondsLength;

            _waveFormat = _audioClient.MixFormat;
        }

        /// <summary>
        /// Current Capturing State
        /// </summary>
        public CaptureState CaptureState => _captureState;

        /// <summary>
        /// Capturing wave format
        /// </summary>
        public virtual WaveFormat WaveFormat
        {
            get
            {
                // for convenience, return a WAVEFORMATEX, instead of the real
                // WAVEFORMATEXTENSIBLE being used
                return _waveFormat;
            }
            set { _waveFormat = value; }
        }

        /// <summary>
        /// Gets the default audio capture device
        /// </summary>
        /// <returns>The default audio capture device</returns>
        public static WasapiAudioDevice DefaultDevice => WasapiAudioDevice.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);

        /// <summary>
        /// Enumerates all Wasapi Capture Devices.
        /// </summary>
        public static IEnumerable<WasapiAudioDevice> EnumerateDevices() => WasapiAudioDevice.EnumerateAudioEndPoints(DataFlow.Capture);

        void Init()
        {
            if (_initialized)
                return;

            var requestedDuration = ReftimesPerMillisec * _audioBufferMillisecondsLength;

            if (!_audioClient.IsFormatSupported(AudioClientShareMode.Shared, _waveFormat))
                throw new ArgumentException("Unsupported Wave Format");

            var streamFlags = AudioClientStreamFlags;

            _audioClient.Initialize(AudioClientShareMode.Shared,
                streamFlags,
                requestedDuration,
                0,
                _waveFormat,
                Guid.Empty);
            
            var bufferFrameCount = _audioClient.BufferSize;
            _bytesPerFrame = _waveFormat.Channels * _waveFormat.BitsPerSample / 8;
            _recordBuffer = new byte[bufferFrameCount * _bytesPerFrame];

            _initialized = true;
        }

        /// <summary>
        /// To allow overrides to specify different flags (e.g. loopback)
        /// </summary>
        protected virtual int AudioClientStreamFlags => 0;

        /// <summary>
        /// Start Capturing
        /// </summary>
        public virtual void Start()
        {
            if (_captureState != CaptureState.Stopped)
                throw new InvalidOperationException("Previous recording still in progress");

            _captureState = CaptureState.Starting;
            Init();
            ThreadStart start = () => CaptureThread(_audioClient);
            _captureThread = new Thread(start);
            _captureThread.Start();
        }

        /// <summary>
        /// Stop Capturing (requests a stop, wait for <see cref="RecordingStopped"/> event to know it has finished)
        /// </summary>
        public virtual void Stop()
        {
            if (_captureState != CaptureState.Stopped)
                _captureState = CaptureState.Stopping;
        }

        void CaptureThread(AudioClient Client)
        {
            Exception exception = null;
            try { DoRecording(Client); }
            catch (Exception e) { exception = e; }
            finally
            {
                Client.Stop();
                // don't dispose - the AudioClient only gets disposed when WasapiCapture is disposed
            }

            _captureThread = null;
            _captureState = CaptureState.Stopped;
            RaiseRecordingStopped(exception);
        }

        void DoRecording(AudioClient Client)
        {
            var bufferFrameCount = Client.BufferSize;

            // Calculate the actual duration of the allocated buffer.
            var actualDuration = (long)((double)ReftimesPerSec *
                             bufferFrameCount / _waveFormat.SampleRate);

            var sleepMilliseconds = (int)(actualDuration / ReftimesPerMillisec / 2);

            var capture = Client.AudioCaptureClient;

            Client.Start();
            _captureState = CaptureState.Capturing;

            while (_captureState == CaptureState.Capturing)
            {
                Thread.Sleep(sleepMilliseconds);

                if (_captureState != CaptureState.Capturing)
                    break;

                ReadNextPacket(capture);
            }
        }

        void RaiseRecordingStopped(Exception e)
        {
            var handler = RecordingStopped;

            if (handler == null)
                return;

            if (_syncContext == null)
                handler(e);

            else _syncContext.Post(State => handler(e), null);
        }

        void ReadNextPacket(AudioCaptureClient Capture)
        {
            int packetSize = Capture.GetNextPacketSize(),
                recordBufferOffset = 0;

            while (packetSize != 0)
            {
                int framesAvailable, flags;
                var buffer = Capture.GetBuffer(out framesAvailable, out flags);

                var bytesAvailable = framesAvailable * _bytesPerFrame;

                // apparently it is sometimes possible to read more frames than we were expecting?
                var spaceRemaining = Math.Max(0, _recordBuffer.Length - recordBufferOffset);
                if (spaceRemaining < bytesAvailable && recordBufferOffset > 0)
                {
                    DataAvailable?.Invoke(_recordBuffer, recordBufferOffset);
                    recordBufferOffset = 0;
                }

                const int audioClientBufferFlagsSilent = 0x2;

                // if not silence...
                if ((flags & audioClientBufferFlagsSilent) != audioClientBufferFlagsSilent)
                    Marshal.Copy(buffer, _recordBuffer, recordBufferOffset, bytesAvailable);
                else Array.Clear(_recordBuffer, recordBufferOffset, bytesAvailable);

                recordBufferOffset += bytesAvailable;
                Capture.ReleaseBuffer(framesAvailable);
                packetSize = Capture.GetNextPacketSize();
            }

            DataAvailable?.Invoke(_recordBuffer, recordBufferOffset);
        }

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public virtual void Dispose()
        {
            Stop();

            _captureThread?.Join();
            _captureThread = null;
            
            _audioClient?.Dispose();
            _audioClient = null;
        }

        /// <summary>
        /// <see cref="WasapiCapture"/> is not synchronizable.
        /// </summary>
        public bool IsSynchronizable => false;
    }
}

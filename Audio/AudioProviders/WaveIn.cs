using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Screna.Audio
{
    /// <summary>
    /// WaveIn Device.
    /// </summary>
    public class WaveInDevice
    {
        /// <summary>
        /// Gets the Device ID.
        /// </summary>
        public int DeviceNumber { get; }

        /// <summary>
        /// Creates a new <see cref="WaveInDevice"/> object.
        /// </summary>
        /// <param name="DeviceNumber">Device ID.</param>
        public WaveInDevice(int DeviceNumber) { this.DeviceNumber = DeviceNumber; }

        /// <summary>
        /// Gets the Device Name.
        /// </summary>
        public string Name => GetCapabilities(DeviceNumber).ProductName;

        /// <summary>
        /// Returns the number of Wave In devices available in the system
        /// </summary>
        public static int DeviceCount => WaveInterop.waveInGetNumDevs();

        /// <summary>
        /// Retrieves the capabilities of a waveIn device
        /// </summary>
        /// <param name="DevNumber">Device to test</param>
        /// <returns>The WaveIn device capabilities</returns>
        static WaveInCapabilities GetCapabilities(int DevNumber)
        {
            var caps = new WaveInCapabilities();
            var structSize = Marshal.SizeOf(caps);
            MmException.Try(WaveInterop.waveInGetDevCaps((IntPtr)DevNumber, out caps, structSize), "waveInGetDevCaps");
            return caps;
        }

        /// <summary>
        /// Checks to see if a given SupportedWaveFormat is supported
        /// </summary>
        /// <param name="WaveFormat">The SupportedWaveFormat</param>
        /// <returns>true if supported</returns>
        public bool SupportsWaveFormat(SupportedWaveFormat WaveFormat) => GetCapabilities(DeviceNumber).SupportedFormats.HasFlag(WaveFormat);

        /// <summary>
        /// Enumerates all <see cref="WaveInDevice"/>(s).
        /// </summary>
        public static IEnumerable<WaveInDevice> Enumerate()
        {
            var n = DeviceCount;

            for (var i = 0; i < n; i++)
                yield return new WaveInDevice(i);
        }

        /// <summary>
        /// Gets the Default <see cref="WaveInDevice"/>.
        /// </summary>
        public static WaveInDevice DefaultDevice => new WaveInDevice(0);
    }

    /// <summary>
    /// Recording using waveIn api with event callbacks.
    /// Events are raised as recorded buffers are made available
    /// </summary>
    public class WaveIn : IAudioProvider
    {
        readonly AutoResetEvent _callbackEvent;
        readonly SynchronizationContext _syncContext;
        readonly int _deviceNumber;
        IntPtr _waveInHandle;
        volatile bool _recording;
        WaveInBuffer[] _buffers;

        /// <summary>
        /// Indicates recorded data is available 
        /// </summary>
        public event Action<byte[], int> DataAvailable;

        /// <summary>
        /// Indicates that all recorded data has now been received.
        /// </summary>
        public event Action<Exception> RecordingStopped;

        /// <summary>
        /// Creates a new instance of <see cref="WaveIn"/>.
        /// </summary>
        public WaveIn(int DeviceNumber = 0)
        {
            _callbackEvent = new AutoResetEvent(false);
            _syncContext = SynchronizationContext.Current;
            _deviceNumber = DeviceNumber;
            WaveFormat = new WaveFormat(8000, 16, 1);
            BufferMilliseconds = 100;
            NumberOfBuffers = 3;
            IsSynchronizable = false;
        }

        /// <summary>
        /// Creates a new instance of <see cref="WaveIn"/> to be used with Video capture.
        /// </summary>
        /// <param name="DeviceNumber">Device to use.</param>
        /// <param name="FrameRate">Frame Rate of video capture.</param>
        /// <param name="Wf">Audio Wave format.</param>
        public WaveIn(int DeviceNumber, int FrameRate, WaveFormat Wf) : this(DeviceNumber)
        {
            WaveFormat = Wf;
            // Buffer size to store duration of 1 frame 
            BufferMilliseconds = (int)Math.Ceiling(1000 / (decimal)FrameRate);
            IsSynchronizable = true;
        }

        /// <summary>
        /// Milliseconds for the buffer. Recommended value is 100ms
        /// </summary>
        public int BufferMilliseconds { get; set; }

        /// <summary>
        /// Number of Buffers to use (usually 2 or 3)
        /// </summary>
        public int NumberOfBuffers { get; set; }

        void CreateBuffers()
        {
            // Default to three buffers of 100ms each
            var bufferSize = BufferMilliseconds * WaveFormat.AverageBytesPerSecond / 1000;
            if (bufferSize % WaveFormat.BlockAlign != 0)
                bufferSize -= bufferSize % WaveFormat.BlockAlign;

            _buffers = new WaveInBuffer[NumberOfBuffers];
            for (var n = 0; n < _buffers.Length; n++)
                _buffers[n] = new WaveInBuffer(_waveInHandle, bufferSize);
        }

        void OpenWaveInDevice()
        {
            const int callbackEvent = 0x50000;

            CloseWaveInDevice();
            var result = WaveInterop.waveInOpen(out _waveInHandle, (IntPtr)_deviceNumber, WaveFormat,
                _callbackEvent.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, callbackEvent);
            MmException.Try(result, "waveInOpen");
            CreateBuffers();
        }

        /// <summary>
        /// Start recording.
        /// </summary>
        public void Start()
        {
            if (_recording)
                throw new InvalidOperationException("Already recording");
            OpenWaveInDevice();
            MmException.Try(WaveInterop.waveInStart(_waveInHandle), "waveInStart");
            _recording = true;
            ThreadPool.QueueUserWorkItem(state => RecordThread(), null);
        }

        void RecordThread()
        {
            Exception exception = null;
            try { DoRecording(); }
            catch (Exception e) { exception = e; }
            finally
            {
                _recording = false;
                RaiseRecordingStoppedEvent(exception);
            }
        }

        void DoRecording()
        {
            foreach (var buffer in _buffers.Where(Buffer => !Buffer.InQueue))
                buffer.Reuse();

            while (_recording)
            {
                if (!_callbackEvent.WaitOne())
                    continue;

                // requeue any buffers returned to us
                if (!_recording)
                    continue;

                foreach (var buffer in _buffers.Where(Buffer => Buffer.Done))
                {
                    DataAvailable?.Invoke(buffer.Data, buffer.BytesRecorded);

                    buffer.Reuse();
                }
            }
        }

        void RaiseRecordingStoppedEvent(Exception e)
        {
            var handler = RecordingStopped;

            if (handler == null)
                return;

            if (_syncContext == null)
                handler(e);

            else _syncContext.Post(State => handler(e), null);
        }

        /// <summary>
        /// Stop Recording.
        /// </summary>
        public void Stop()
        {
            _recording = false;
            _callbackEvent.Set(); // signal the thread to exit
            MmException.Try(WaveInterop.waveInStop(_waveInHandle), "waveInStop");
        }

        /// <summary>
        /// WaveFormat we are recording in
        /// </summary>
        public WaveFormat WaveFormat { get; set; }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool Disposing)
        {
            if (!Disposing)
                return;

            if (_recording)
                Stop();

            CloseWaveInDevice();
        }

        void CloseWaveInDevice()
        {
            // Some drivers need the reset to properly release buffers
            WaveInterop.waveInReset(_waveInHandle);

            if (_buffers != null)
            {
                foreach (var t in _buffers)
                    t.Dispose();

                _buffers = null;
            }

            WaveInterop.waveInClose(_waveInHandle);
            _waveInHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// <see cref="WaveIn"/> is synchronizable.
        /// </summary>
        public bool IsSynchronizable { get; }
    }
}

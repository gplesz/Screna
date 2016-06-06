using System;
using System.Runtime.InteropServices;
using ManagedBass;
using Screna.Audio;
using BASS = ManagedBass.Bass;
using WaveFormat = Screna.Audio.WaveFormat;

namespace Screna.Bass
{
    /// <summary>
    /// Provides audio from Microphone.
    /// </summary>
    public class RecordingProvider : IAudioProvider
    {
        /// <summary>
        /// Creates a new instance of <see cref="RecordingProvider"/> using Default Device and <see cref="WaveFormat"/>.
        /// </summary>
        public RecordingProvider()
            : this(RecordingDevice.DefaultDevice) { }

        /// <summary>
        /// Creates a new instance of <see cref="RecordingProvider"/> using Default <see cref="WaveFormat"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        public RecordingProvider(RecordingDevice Device)
            : this(Device, new WaveFormat()) { }

        /// <summary>
        /// Creates a new instance of <see cref="RecordingProvider"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        /// <param name="Wf"><see cref="WaveFormat"/> to use.</param>
        public RecordingProvider(RecordingDevice Device, WaveFormat Wf)
            : this(Device, Wf, -1) { }

        /// <summary>
        /// Creates a new synchronizable instance of <see cref="RecordingProvider"/> to be used with an <see cref="IRecorder"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        /// <param name="Wf"><see cref="WaveFormat"/> to use.</param>
        /// <param name="FrameRate">The <see cref="IRecorder"/>'s FrameRate.</param>
        public RecordingProvider(RecordingDevice Device, WaveFormat Wf, int FrameRate)
        {
            WaveFormat = Wf;
            
            BASS.RecordInit(Device.DeviceIndex);

            BASS.CurrentRecordingDevice = Device.DeviceIndex;

            var flags = BassFlags.RecordPause;
            
            if (Wf.Encoding == WaveFormatEncoding.Float && Wf.BitsPerSample == 32)
                flags |= BassFlags.Float;
            
            else if (Wf.Encoding == WaveFormatEncoding.Pcm && Wf.BitsPerSample == 8)
                flags |= BassFlags.Byte;
            
            else if (!(Wf.Encoding == WaveFormatEncoding.Pcm && Wf.BitsPerSample == 16))
                throw new ArgumentException(nameof(Wf));

            IsSynchronizable = FrameRate != -1;

            if (IsSynchronizable)
                BASS.RecordingBufferLength = 3000 / FrameRate;
            
            _handle = IsSynchronizable ? BASS.RecordStart(Wf.SampleRate, Wf.Channels, flags, BASS.RecordingBufferLength / 3, Procedure, IntPtr.Zero)
                                      : BASS.RecordStart(Wf.SampleRate, Wf.Channels, flags, Procedure);

            BASS.ChannelSetSync(_handle, SyncFlags.Free, 0, (H, C, D, U) => RecordingStopped?.Invoke(this, new EndEventArgs(null)));
        }

        readonly int _handle;

        /// <summary>
        /// Frees up the resources used by this instant.
        /// </summary>
        public void Dispose() => BASS.StreamFree(_handle);

        /// <summary>
        /// Start Recording.
        /// </summary>
        public void Start() => BASS.ChannelPlay(_handle);

        /// <summary>
        /// Stop Recording.
        /// </summary>
        public void Stop() => BASS.ChannelPause(_handle);

        /// <summary>
        /// Gets whether this <see cref="IAudioProvider"/> is Synchronizable.
        /// </summary>
        public bool IsSynchronizable { get; }

        /// <summary>
        /// Gets the output <see cref="WaveFormat"/>.
        /// </summary>
        public WaveFormat WaveFormat { get; }

        byte[] _buffer;

        bool Procedure(int HRecord, IntPtr Buffer, int Length, IntPtr User)
        {
            if (_buffer == null || _buffer.Length < Length)
                _buffer = new byte[Length];

            Marshal.Copy(Buffer, _buffer, 0, Length);

            DataAvailable?.Invoke(this, new DataAvailableEventArgs(_buffer, Length));

            return true;
        }

        /// <summary>
        /// Indicates recorded data is available.
        /// </summary>
        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        /// <summary>
        /// Indicates that all recorded data has now been received.
        /// </summary>
        public event EventHandler<EndEventArgs> RecordingStopped;
    }
}
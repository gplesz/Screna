using System;
using NAudio.Wave;
using Screna.Audio;
using WaveFormat = Screna.Audio.WaveFormat;
using NWaveFormat = NAudio.Wave.WaveFormat;

namespace Screna.NAudio
{
    /// <summary>
    /// Provides audio from Microphone using WaveIn API.
    /// </summary>
    public class WaveInProvider : IAudioProvider
    {
        readonly WaveInEvent _waveInEvent;

        /// <summary>
        /// Creates a new instance of <see cref="WaveInProvider"/> using Default Device and <see cref="WaveFormat"/>.
        /// </summary>
        public WaveInProvider() : this(WaveInDevice.DefaultDevice) { }

        /// <summary>
        /// Creates a new instance of <see cref="WaveInProvider"/> using Default <see cref="WaveFormat"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        public WaveInProvider(WaveInDevice Device)
            : this(Device, new WaveFormat()) { }

        /// <summary>
        /// Creates a new instance of <see cref="WaveInProvider"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        /// <param name="Wf"><see cref="WaveFormat"/> to use.</param>
        public WaveInProvider(WaveInDevice Device, WaveFormat Wf)
            : this(Device, Wf, -1) { }

        /// <summary>
        /// Creates a new synchronizable instance of <see cref="WaveInProvider"/> to be used with an <see cref="IRecorder"/>.
        /// </summary>
        /// <param name="Device">The Recording Device.</param>
        /// <param name="Wf"><see cref="WaveFormat"/> to use.</param>
        /// <param name="FrameRate">The <see cref="IRecorder"/>'s FrameRate.</param>
        public WaveInProvider(WaveInDevice Device, WaveFormat Wf, int FrameRate)
        {
            IsSynchronizable = FrameRate != -1;

            _waveInEvent = new WaveInEvent
            {
                DeviceNumber = Device.DeviceNumber,
                BufferMilliseconds = IsSynchronizable ? (int)Math.Ceiling(1000 / (decimal)FrameRate) : 100,
                NumberOfBuffers = 3,
                WaveFormat = new NWaveFormat(Wf.SampleRate, Wf.BitsPerSample, Wf.Channels)
            };
            
            WaveFormat = Wf;

            _waveInEvent.RecordingStopped += (Sender, Args) => RecordingStopped?.Invoke(this, new EndEventArgs(Args.Exception));

            _waveInEvent.DataAvailable += (Sender, Args) => DataAvailable?.Invoke(this, new DataAvailableEventArgs(Args.Buffer, Args.BytesRecorded));
        }
        
        /// <summary>
        /// Gets whether this <see cref="IAudioProvider"/> is Synchronizable.
        /// </summary>
        public bool IsSynchronizable { get; }

        /// <summary>
        /// Gets the output <see cref="WaveFormat"/>.
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Indicates recorded data is available.
        /// </summary>
        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        /// <summary>
        /// Indicates that all recorded data has now been received.
        /// </summary>
        public event EventHandler<EndEventArgs> RecordingStopped;

        /// <summary>
        /// Frees up the resources used by this instant.
        /// </summary>
        public void Dispose() => _waveInEvent?.Dispose();

        /// <summary>
        /// Start Recording.
        /// </summary>
        public void Start() => _waveInEvent?.StartRecording();

        /// <summary>
        /// Stop Recording.
        /// </summary>
        public void Stop() => _waveInEvent?.StopRecording();
    }
}

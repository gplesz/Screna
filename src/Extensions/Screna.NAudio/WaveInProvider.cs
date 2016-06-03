using System;
using NAudio.Wave;
using Screna.Audio;
using WaveFormat = Screna.Audio.WaveFormat;
using NWaveFormat = NAudio.Wave.WaveFormat;

namespace Screna.NAudio
{
    public class WaveInProvider : IAudioProvider
    {
        readonly WaveInEvent _waveInEvent;

        public WaveInProvider() : this(WaveInDevice.DefaultDevice) { }

        public WaveInProvider(WaveInDevice Device)
            : this(Device, new WaveFormat(8000, 16, 1)) { }

        public WaveInProvider(WaveInDevice Device, WaveFormat Wf)
        {
            _waveInEvent = new WaveInEvent
            {
                DeviceNumber = Device.DeviceNumber,
                BufferMilliseconds = 100,
                NumberOfBuffers = 3,
                WaveFormat = new NWaveFormat(Wf.SampleRate, Wf.BitsPerSample, Wf.Channels)
            };

            IsSynchronizable = false;
            WaveFormat = Wf;

            Setup();
        }

        public WaveInProvider(WaveInDevice Device, int FrameRate, WaveFormat Wf)
        {
            _waveInEvent = new WaveInEvent
            {
                DeviceNumber = Device.DeviceNumber,
                BufferMilliseconds = (int)Math.Ceiling(1000 / (decimal)FrameRate),
                NumberOfBuffers = 3,
                WaveFormat = new NWaveFormat(Wf.SampleRate, Wf.BitsPerSample, Wf.Channels)
            };

            IsSynchronizable = true;
            WaveFormat = Wf;

            Setup();
        }

        void Setup()
        {
            _waveInEvent.RecordingStopped += (Sender, Args) => RecordingStopped?.Invoke(this, new EndEventArgs(Args.Exception));

            _waveInEvent.DataAvailable += (Sender, Args) => DataAvailable?.Invoke(this, new DataAvailableEventArgs(Args.Buffer, Args.BytesRecorded));
        }

        public bool IsSynchronizable { get; }

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

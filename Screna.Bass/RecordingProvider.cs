using System;
using System.Runtime.InteropServices;
using ManagedBass;
using Screna.Audio;
using BASS = ManagedBass.Bass;
using WaveFormat = Screna.Audio.WaveFormat;

namespace Screna.Bass
{
    public class RecordingProvider : IAudioProvider
    {
        readonly RecordProcedure _proc;

        public RecordingProvider(RecordingDevice Device, WaveFormat Wf, int FrameRate = -1)
        {
            WaveFormat = Wf;
            _proc = Procedure;

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

            Handle = IsSynchronizable ? BASS.RecordStart(Wf.SampleRate, Wf.Channels, flags, BASS.RecordingBufferLength / 3, _proc, IntPtr.Zero)
                                      : BASS.RecordStart(Wf.SampleRate, Wf.Channels, flags, _proc);

            BASS.ChannelSetSync(Handle, SyncFlags.Free, 0, (H, C, D, U) => RecordingStopped?.Invoke(this, new EndEventArgs(null)));
        }

        protected int Handle;

        public void Dispose() => BASS.StreamFree(Handle);

        public void Start() => BASS.ChannelPlay(Handle);

        public void Stop() => BASS.ChannelPause(Handle);
        
        public bool IsSynchronizable { get; }
        
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

        public event EventHandler<DataAvailableEventArgs> DataAvailable;
        
        public event EventHandler<EndEventArgs> RecordingStopped;
    }
}
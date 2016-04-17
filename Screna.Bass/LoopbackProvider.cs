using System;
using System.Linq;
using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.Wasapi;
using Screna.Audio;
using WaveFormat = Screna.Audio.WaveFormat;

namespace Screna.Bass
{
    public class LoopbackProvider : IAudioProvider
    {
        readonly Silence _silencePlayer;
        readonly int _deviceIndex;
        readonly WasapiProcedure _proc;

        public LoopbackProvider(WasapiLoopbackDevice Device, bool IncludeSilence = true)
        {
            _deviceIndex = Device.DeviceIndex;
            _proc = Procedure;
            
            if (IncludeSilence)
                _silencePlayer = new Silence(PlaybackDevice.Devices.First(Dev => Dev.DeviceInfo.Driver == Device.DeviceInfo.ID));

            BassWasapi.Init(_deviceIndex, Procedure: _proc);
            BassWasapi.CurrentDevice = Device.DeviceIndex;

            var info = BassWasapi.Info;

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(info.Frequency, info.Channels);
        }

        public bool IsSynchronizable => false;
        
        public WaveFormat WaveFormat { get; }

        public event EventHandler<DataAvailableEventArgs> DataAvailable;
        
        public event EventHandler<EndEventArgs> RecordingStopped;

        byte[] _buffer;

        int Procedure(IntPtr Buffer, int Length, IntPtr User)
        {
            if (_buffer == null || _buffer.Length < Length)
                _buffer = new byte[Length];

            Marshal.Copy(Buffer, _buffer, 0, Length);

            DataAvailable?.Invoke(this, new DataAvailableEventArgs(_buffer, Length));

            return Length;
        }

        public void Dispose()
        {
            BassWasapi.CurrentDevice = _deviceIndex;
            BassWasapi.Free();
            _silencePlayer?.Dispose();
        }

        public void Start()
        {
            _silencePlayer?.Start();

            BassWasapi.CurrentDevice = _deviceIndex;

            if (!BassWasapi.Start())
                _silencePlayer?.Stop();
        }

        public void Stop()
        {
            _silencePlayer?.Stop();

            BassWasapi.CurrentDevice = _deviceIndex;

            BassWasapi.Stop();
        }
    }
}
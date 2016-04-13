using System;
using System.Collections.Generic;

namespace Screna.Audio
{
    /// <summary>
    /// Loopback Capture using Wasapi
    /// </summary>
    public sealed class WasapiLoopbackCapture : WasapiCapture
    {
        readonly WasapiSilenceOut _silencePlayer;

        /// <summary>
        /// Creates a new instance of <see cref="WasapiLoopbackCapture"/> using <see cref="DefaultDevice"/>.
        /// </summary>
        /// <param name="IncludeSilence">Whether to record Silence.</param>
        public WasapiLoopbackCapture(bool IncludeSilence) : this(DefaultDevice, IncludeSilence) { }

        /// <summary>
        /// Initialises a new instance of the WASAPI capture class
        /// </summary>
        /// <param name="LoopbackDevice">Capture device to use</param>
        /// <param name="IncludeSilence">Whether to record Silence.</param>
        public WasapiLoopbackCapture(WasapiAudioDevice LoopbackDevice, bool IncludeSilence = true)
            : base(LoopbackDevice)
        {
            if (IncludeSilence)
                _silencePlayer = new WasapiSilenceOut(LoopbackDevice, 100);
        }

        /// <summary>
        /// Starts Capture.
        /// </summary>
        public override void Start()
        {
            _silencePlayer?.Play();

            base.Start();
        }

        /// <summary>
        /// Stops Capture.
        /// </summary>
        public override void Stop()
        {
            base.Stop();

            _silencePlayer?.Stop();
        }

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();

            if (_silencePlayer == null)
                return;

            _silencePlayer.Dispose();
            _silencePlayer.Stop();
        }

        /// <summary>
        /// Gets the default audio loopback capture device
        /// </summary>
        /// <returns>The default audio loopback capture device</returns>
        public new static WasapiAudioDevice DefaultDevice => WasapiAudioDevice.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        /// <summary>
        /// Enumerates all Wasapi Loopback Devices.
        /// </summary>
        public new static IEnumerable<WasapiAudioDevice> EnumerateDevices() => WasapiAudioDevice.EnumerateAudioEndPoints(DataFlow.Render);

        /// <summary>
        /// Capturing wave format
        /// </summary>
        public override WaveFormat WaveFormat
        {
            get { return base.WaveFormat; }
            set { throw new InvalidOperationException("WaveFormat cannot be set for WASAPI Loopback Capture"); }
        }

        /// <summary>
        /// Specify loopback
        /// </summary>
        protected override int AudioClientStreamFlags => 0x00020000;
    }
}

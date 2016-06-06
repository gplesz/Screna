using System.Collections.Generic;
using NAudio.Wave;

namespace Screna.NAudio
{
    /// <summary>
    /// WaveIn Device to be used with <see cref="WaveInProvider"/>.
    /// </summary>
    public class WaveInDevice
    {
        /// <summary>
        /// Gets the Device Number.
        /// </summary>
        public int DeviceNumber { get; }

        /// <summary>
        /// Creates a new instance <see cref="WaveInDevice"/>.
        /// </summary>
        /// <param name="DeviceNumber">Index of the Device.</param>
        public WaveInDevice(int DeviceNumber)
        {
            this.DeviceNumber = DeviceNumber;
        }

        /// <summary>
        /// Gets the Device Name.
        /// </summary>
        public string Name => WaveIn.GetCapabilities(DeviceNumber).ProductName;

        /// <summary>
        /// Gets the no of available WaveIn Devices.
        /// </summary>
        public static int DeviceCount => WaveIn.DeviceCount;

        /// <summary>
        /// Checks if a WaveFormat is supported by the device.
        /// </summary>
        /// <param name="WaveFormat">The WaveFormat to check.</param>
        /// <returns>true if supported, else false.</returns>
        public bool SupportsWaveFormat(SupportedWaveFormat WaveFormat) => WaveIn.GetCapabilities(DeviceNumber).SupportsWaveFormat(WaveFormat);

        /// <summary>
        /// Enumerates WaveIn Devices.
        /// </summary>
        public static IEnumerable<WaveInDevice> Enumerate()
        {
            var n = DeviceCount;

            for (var i = 0; i < n; ++i)
                yield return new WaveInDevice(i);
        } 

        /// <summary>
        /// Gets the Default WaveIn Device.
        /// </summary>
        public static WaveInDevice DefaultDevice => new WaveInDevice(0);
        
        /// <summary>
        /// Returns the Name of the Device.
        /// </summary>
        public override string ToString() => Name;
    }
}

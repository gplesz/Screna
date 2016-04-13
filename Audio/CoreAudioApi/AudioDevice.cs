using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Screna.Audio
{
    /// <summary>
    /// Represents a Wasapi Device.
    /// </summary>
    public class WasapiAudioDevice
    {
        #region MMDeviceEnumerator
        static readonly IMMDeviceEnumerator RealEnumerator;

        static WasapiAudioDevice() { RealEnumerator = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator; }

        /// <summary>
        /// Enumerate Audio Endpoints
        /// </summary>
        /// <param name="DataFlow">Desired DataFlow</param>
        /// <returns>Device Collection</returns>
        internal static IEnumerable<WasapiAudioDevice> EnumerateAudioEndPoints(DataFlow DataFlow)
        {
            IMMDeviceCollection collection;
            const int deviceStateActive = 0x00000001;
            Marshal.ThrowExceptionForHR(RealEnumerator.EnumAudioEndpoints(DataFlow, deviceStateActive, out collection));

            int count;
            Marshal.ThrowExceptionForHR(collection.GetCount(out count));

            for (var index = 0; index < count; index++)
            {
                IMMDevice dev;
                collection.Item(index, out dev);
                yield return new WasapiAudioDevice(dev);
            }
        }

        /// <summary>
        /// Get Default Endpoint
        /// </summary>
        /// <param name="DataFlow">Data Flow</param>
        /// <param name="Role">Role</param>
        /// <returns>Device</returns>
        internal static WasapiAudioDevice GetDefaultAudioEndpoint(DataFlow DataFlow, Role Role)
        {
            IMMDevice device;
            Marshal.ThrowExceptionForHR(RealEnumerator.GetDefaultAudioEndpoint(DataFlow, Role, out device));
            return new WasapiAudioDevice(device);
        }

        /// <summary>
        /// Get device by ID
        /// </summary>
        public static WasapiAudioDevice Get(string Id)
        {
            IMMDevice device;
            Marshal.ThrowExceptionForHR(RealEnumerator.GetDevice(Id, out device));
            return new WasapiAudioDevice(device);
        }
        #endregion

        readonly IMMDevice _deviceInterface;

        static Guid _iidIAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

        #region Properties
        internal AudioClient AudioClient
        {
            get
            {
                object result;
                const int clsCtxAll = 0x1 | 0x2 | 0x4 | 0x10;
                Marshal.ThrowExceptionForHR(_deviceInterface.Activate(ref _iidIAudioClient, clsCtxAll, IntPtr.Zero, out result));
                return new AudioClient(result as IAudioClient);
            }
        }

        /// <summary>
        /// Gets the Device Name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the Device ID.
        /// </summary>
        public string ID
        {
            get
            {
                string result;
                Marshal.ThrowExceptionForHR(_deviceInterface.GetId(out result));
                return result;
            }
        }
        #endregion

        static readonly PropertyKey PkeyDeviceFriendlyName = new PropertyKey
        {
            FormatId = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
            PropertyId = 14
        };

        internal WasapiAudioDevice(IMMDevice RealDevice)
        {
            _deviceInterface = RealDevice;

            IPropertyStore propstore;
            const int storageAccessModeRead = 0;

            Marshal.ThrowExceptionForHR(_deviceInterface.OpenPropertyStore(storageAccessModeRead, out propstore));

            Name = propstore.Read(PkeyDeviceFriendlyName);
        }

        /// <summary>
        /// Returns the <see cref="Name"/> of this <see cref="WasapiAudioDevice"/>.
        /// </summary>
        public override string ToString() => Name;
    }
}

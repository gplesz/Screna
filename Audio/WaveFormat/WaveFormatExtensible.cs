using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Screna.Audio
{
    /// <summary>
    /// WaveFormat Extensible
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
    public class WaveFormatExtensible : WaveFormat
    {
        short wValidBitsPerSample; // bits of precision, or is wSamplesPerBlock if wBitsPerSample==0
        int dwChannelMask; // which channels are present in stream
        Guid subFormat;

        static readonly Guid MEDIASUBTYPE_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71"),
            MEDIASUBTYPE_IEEE_FLOAT = new Guid("00000003-0000-0010-8000-00aa00389b71");

        /// <summary>
        /// Creates a new instance of <see cref="WaveFormatExtensible"/> for PCM or IEEE.
        /// </summary>
        public WaveFormatExtensible(int Rate, int Bits, int channels)
            : base(Rate, Bits, channels)
        {
            waveFormatTag = WaveFormatEncoding.Extensible;
            extraSize = 22;
            wValidBitsPerSample = (short)Bits;

            for (var i = 0; i < channels; i++)
                dwChannelMask |= 1 << i;

            subFormat = Bits == 32 ? MEDIASUBTYPE_IEEE_FLOAT : MEDIASUBTYPE_PCM;
        }

        /// <summary>
        /// Serialize
        /// </summary>
        public override void Serialize(BinaryWriter Writer)
        {
            base.Serialize(Writer);
            Writer.Write(wValidBitsPerSample);
            Writer.Write(dwChannelMask);
            var guid = subFormat.ToByteArray();
            Writer.Write(guid, 0, guid.Length);
        }
    }
}

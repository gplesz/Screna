using System;
using System.IO;
using Screna.Audio;
using SharpAviLame = SharpAvi.Codecs.Mp3AudioEncoderLame;

namespace Screna.Lame
{
    /// <summary>
    /// Mpeg Layer 3 (MP3) audio encoder using the lameenc32.dll (x86) or lameenc64.dll (x64).
    /// </summary>
    /// <remarks>
    /// Only 16-bit input audio is currently supported.
    /// The class is designed for using only a single instance at a time.
    /// Find information about and downloads of the LAME project at http://lame.sourceforge.net/.
    /// </remarks>
    public class Mp3EncoderLame : IAudioEncoder
    {
        /// <summary>
        /// Supported output bit rates (in kilobits per second).
        /// </summary>
        /// <remarks>
        /// Currently supported are 64, 96, 128, 160, 192 and 320 kbps.
        /// </remarks>
        public static int[] SupportedBitRates => SharpAviLame.SupportedBitRates;

        readonly SharpAviLame _sharpAviLame;
        
        #region Constructors
        static Mp3EncoderLame()
        {
            SharpAviLame.SetLameDllLocation(Path.Combine(Path.GetDirectoryName(typeof(Mp3EncoderLame).Assembly.Location), $"lameenc{(Environment.Is64BitProcess ? 64 : 32)}.dll"));
        }

        /// <summary>
        /// Creates a new instance of <see cref="Mp3EncoderLame"/>.
        /// </summary>
        /// <param name="ChannelCount">Channel count.</param>
        /// <param name="SampleRate">Sample rate (in samples per second).</param>
        /// <param name="OutputBitRateKbps">Output bit rate (in kilobits per second).</param>
        /// <remarks>
        /// Encoder expects audio data in 16-bit samples.
        /// Stereo data should be interleaved: left sample first, right sample second.
        /// </remarks>
        public Mp3EncoderLame(int ChannelCount = 1, int SampleRate = 44100, int OutputBitRateKbps = 160)
        {
            _sharpAviLame = new SharpAviLame(ChannelCount, SampleRate, OutputBitRateKbps);
            
            WaveFormat = new WaveFormatExtra(SampleRate, 16, ChannelCount, _sharpAviLame.FormatSpecificData)
            {
                Encoding = WaveFormatEncoding.Mp3
            };
        }
        #endregion

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose() => _sharpAviLame?.Dispose();

        /// <summary>
        /// Encodes block of audio data.
        /// </summary>
        public int Encode(byte[] Source, int SourceOffset, int SourceCount, byte[] Destination, int DestinationOffset)
        {
            return _sharpAviLame.EncodeBlock(Source, SourceOffset, SourceCount, Destination, DestinationOffset);
        }
        
        /// <summary>
        /// Ensures that the buffer is big enough to hold the result of encoding <paramref name="SourceCount"/> bytes.
        /// </summary>
        public void EnsureBufferIsSufficient(ref byte[] Buffer, int SourceCount)
        {
            var maxLength = GetMaxEncodedLength(SourceCount);
            if (Buffer?.Length >= maxLength)
                return;

            var newLength = Buffer?.Length * 2 ?? 1024;

            while (newLength < maxLength)
                newLength *= 2;

            Array.Resize(ref Buffer, newLength);
        }

        /// <summary>
        /// Flushes internal encoder's buffers.
        /// </summary>
        public int Flush(byte[] Destination, int DestinationOffset) => _sharpAviLame.Flush(Destination, DestinationOffset);

        /// <summary>
        /// Gets if RIFF header is needed when writing to a file.
        /// </summary>
        public bool RequiresRiffHeader => false;

        /// <summary>
        /// Gets maximum length of encoded data. Estimate taken from the description of 'lame_encode_buffer' method in 'lame.h'
        /// </summary>
        public int GetMaxEncodedLength(int SourceCount) => _sharpAviLame.GetMaxEncodedLength(SourceCount);

        /// <summary>
        /// Wave Format including Mp3 Specific Data.
        /// </summary>
        public WaveFormat WaveFormat { get; }
    }
}

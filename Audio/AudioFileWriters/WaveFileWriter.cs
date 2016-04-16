using System;
using System.IO;
using static System.Text.Encoding;

namespace Screna.Audio
{
    /// <summary>
    /// Writes Wave data to a .wav file on disk.
    /// </summary>
    public class WaveFileWriter : IAudioFileWriter
    {
        readonly BinaryWriter _writer;
        readonly long _dataSizePos, _factSampleCountPos;

        /// <summary>
        /// Creates a new instance of <see cref="WaveFileWriter"/>.
        /// </summary>
        /// <param name="OutStream">Stream to be written to</param>
        /// <param name="Format">Wave format to use</param>
        /// <exception cref="ArgumentNullException"><paramref name="OutStream"/> or <paramref name="Format"/> is null.</exception>
        public WaveFileWriter(Stream OutStream, WaveFormat Format)
        {
            if (OutStream == null)
                throw new ArgumentNullException(nameof(OutStream));

            if (Format == null)
                throw new ArgumentNullException(nameof(Format));

            WaveFormat = Format;

            _writer = new BinaryWriter(OutStream, UTF8);

            _writer.Write(UTF8.GetBytes("RIFF"));
            _writer.Write(0); // placeholder
            _writer.Write(UTF8.GetBytes("WAVE"));

            _writer.Write(UTF8.GetBytes("fmt "));
            
            _writer.Write(18 + Format.ExtraSize); // wave format length
            Format.Serialize(_writer);

            // CreateFactChunk
            if (HasFactChunk)
            {
                _writer.Write(UTF8.GetBytes("fact"));
                _writer.Write(4);
                _factSampleCountPos = OutStream.Position;
                _writer.Write(0); // number of samples
            }

            // WriteDataChunkHeader
            _writer.Write(UTF8.GetBytes("data"));
            _dataSizePos = OutStream.Position;
            _writer.Write(0); // placeholder
        }

        /// <summary>
        /// Creates a new instance of <see cref="WaveFileWriter"/>.
        /// </summary>
        /// <param name="Filename">The filename to write to</param>
        /// <param name="Format">The Wave Format of the output data</param>
        public WaveFileWriter(string Filename, WaveFormat Format)
            : this(new FileStream(Filename, FileMode.Create, FileAccess.Write, FileShare.Read), Format) { }

        bool HasFactChunk => WaveFormat.Encoding != WaveFormatEncoding.Pcm &&
                             WaveFormat.BitsPerSample != 0;

        /// <summary>
        /// Number of bytes of audio in the data chunk
        /// </summary>
        public long Length { get; private set; }

        /// <summary>
        /// WaveFormat of this wave file
        /// </summary>
        public WaveFormat WaveFormat { get; }
        
        /// <summary>
        /// Appends bytes to the WaveFile (assumes they are already in the correct format)
        /// </summary>
        /// <param name="Data">the buffer containing the wave data</param>
        /// <param name="Offset">the offset from which to start writing</param>
        /// <param name="Count">the number of bytes to write</param>
        public void Write(byte[] Data, int Offset, int Count)
        {
            if (_writer.BaseStream.Length + Count > uint.MaxValue)
                throw new ArgumentException("WAV file too large", nameof(Count));

            _writer.Write(Data, Offset, Count);
            Length += Count;
        }

        /// <summary>
        /// Writes a single sample to the Wave file
        /// </summary>
        /// <param name="Sample">the sample to write (assumed floating point with 1.0f as max value)</param>
        public void WriteSample(float Sample)
        {
            switch (WaveFormat.BitsPerSample)
            {
                case 16:
                    _writer.Write((short)(short.MaxValue * Sample));
                    Length += 2;
                    break;

                case 24:
                    _writer.Write(BitConverter.GetBytes((int)(int.MaxValue * Sample)), 1, 3);
                    Length += 3;
                    break;

                default:
                    if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Extensible)
                    {
                        _writer.Write(ushort.MaxValue * (int)Sample);
                        Length += 4;
                    }
                    else if (WaveFormat.Encoding == WaveFormatEncoding.Float)
                    {
                        _writer.Write(Sample);
                        Length += 4;
                    }
                    else throw new InvalidOperationException("Only 16, 24 or 32 bit PCM or IEEE float audio data supported");
                    break;
            }
        }

        /// <summary>
        /// Writes 32 bit floating point samples to the Wave file
        /// They will be converted to the appropriate bit depth depending on the WaveFormat of the WAV file
        /// </summary>
        /// <param name="Samples">The buffer containing the floating point samples</param>
        /// <param name="Offset">The offset from which to start writing</param>
        /// <param name="Count">The number of floating point samples to write</param>
        public void WriteSamples(float[] Samples, int Offset, int Count)
        {
            for (var n = 0; n < Count; n++)
                WriteSample(Samples[Offset + n]);
        }

        /// <summary>
        /// Writes 16 bit samples to the Wave file
        /// </summary>
        /// <param name="Samples">The buffer containing the 16 bit samples</param>
        /// <param name="Offset">The offset from which to start writing</param>
        /// <param name="Count">The number of 16 bit samples to write</param>
        public void WriteSamples(short[] Samples, int Offset, int Count)
        {
            // 16 bit PCM data
            switch (WaveFormat.BitsPerSample)
            {
                case 16:
                    for (var sample = 0; sample < Count; sample++)
                        _writer.Write(Samples[sample + Offset]);
                    Length += Count * 2;
                    break;

                case 24:
                    for (var sample = 0; sample < Count; sample++)
                        _writer.Write(BitConverter.GetBytes(ushort.MaxValue * Samples[sample + Offset]), 1, 3);
                    
                    Length += Count * 3;
                    break;

                default:
                    if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Extensible)
                    {
                        for (var sample = 0; sample < Count; sample++)
                            _writer.Write(ushort.MaxValue * Samples[sample + Offset]);
                        Length += Count * 4;
                    }

                    // IEEE float data
                    else if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Float)
                    {
                        for (var sample = 0; sample < Count; sample++)
                            _writer.Write(Samples[sample + Offset] / (float)(short.MaxValue + 1));
                        Length += Count * 4;
                    }

                    else throw new InvalidOperationException("Only 16, 24 or 32 bit PCM or IEEE float audio data supported");
                    break;
            }
        }

        /// <summary>
        /// Ensures data is written to disk
        /// Also updates header, so that WAV file will be valid up to the point currently written
        /// </summary>
        public void Flush()
        {
            var pos = _writer.BaseStream.Position;
            UpdateHeader(_writer);
            _writer.BaseStream.Position = pos;
        }

        /// <summary>
        /// Closes the file and frees all resources.
        /// </summary>
        public void Dispose()
        {
            DoDispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Actually performs the close, making sure the header contains the correct data
        /// </summary>
        void DoDispose()
        {
            if (_writer == null)
                return;

            using (_writer)
                UpdateHeader(_writer);
        }

        /// <summary>
        /// Updates the header with file size information
        /// </summary>
        void UpdateHeader(BinaryWriter Writer)
        {
            Writer.Flush();

            // UpdateRiffChunk
            Writer.Seek(4, SeekOrigin.Begin);
            Writer.Write((uint)(_writer.BaseStream.Length - 8));

            // UpdateFactChunk
            if (HasFactChunk)
            {
                var bitsPerSample = WaveFormat.BitsPerSample * WaveFormat.Channels;
                if (bitsPerSample != 0)
                {
                    Writer.Seek((int)_factSampleCountPos, SeekOrigin.Begin);

                    Writer.Write((int)(Length * 8 / bitsPerSample));
                }
            }

            // UpdateDataChunk
            Writer.Seek((int)_dataSizePos, SeekOrigin.Begin);
            Writer.Write((uint)Length);
        }

        /// <summary>
        /// Finaliser - should only be called if the user forgot to close this WaveFileWriter
        /// </summary>
        ~WaveFileWriter() { DoDispose(); }
    }
}

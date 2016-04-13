using System;
using System.IO;

namespace Screna.Audio
{
    /// <summary>
    /// Writes Wave data to a .wav file on disk.
    /// </summary>
    public class WaveFileWriter : IAudioFileWriter
    {
        Stream _outStream;
        readonly BinaryWriter _writer;
        readonly long _dataSizePos, _factSampleCountPos;
        long _dataChunkSize;

        /// <summary>
        /// Creates a new instance of <see cref="WaveFileWriter"/>.
        /// </summary>
        /// <param name="OutStream">Stream to be written to</param>
        /// <param name="Format">Wave format to use</param>
        public WaveFileWriter(Stream OutStream, WaveFormat Format)
        {
            _outStream = OutStream;
            WaveFormat = Format;
            _writer = new BinaryWriter(OutStream, System.Text.Encoding.UTF8);
            _writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            _writer.Write(0); // placeholder
            _writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            _writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            
            _writer.Write(18 + Format.ExtraSize); // wave format length
            Format.Serialize(_writer);

            // CreateFactChunk
            if (HasFactChunk)
            {
                _writer.Write(System.Text.Encoding.UTF8.GetBytes("fact"));
                _writer.Write(4);
                _factSampleCountPos = OutStream.Position;
                _writer.Write(0); // number of samples
            }

            // WriteDataChunkHeader
            _writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
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
        public long Length => _dataChunkSize;

        /// <summary>
        /// WaveFormat of this wave file
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Gets the Position in the WaveFile (i.e. number of bytes written so far)
        /// </summary>
        public long Position => _dataChunkSize;

        /// <summary>
        /// Appends bytes to the WaveFile (assumes they are already in the correct format)
        /// </summary>
        /// <param name="Data">the buffer containing the wave data</param>
        /// <param name="Offset">the offset from which to start writing</param>
        /// <param name="Count">the number of bytes to write</param>
        public void Write(byte[] Data, int Offset, int Count)
        {
            if (_outStream.Length + Count > uint.MaxValue)
                throw new ArgumentException("WAV file too large", nameof(Count));
            _outStream.Write(Data, Offset, Count);
            _dataChunkSize += Count;
        }

        readonly byte[] _value24 = new byte[3]; // keep this around to save us creating it every time

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
                    _dataChunkSize += 2;
                    break;

                case 24:
                    var value = BitConverter.GetBytes((int)(int.MaxValue * Sample));
                    _value24[0] = value[1];
                    _value24[1] = value[2];
                    _value24[2] = value[3];
                    _writer.Write(_value24);
                    _dataChunkSize += 3;
                    break;

                default:
                    if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Extensible)
                    {
                        _writer.Write(ushort.MaxValue * (int)Sample);
                        _dataChunkSize += 4;
                    }
                    else if (WaveFormat.Encoding == WaveFormatEncoding.Float)
                    {
                        _writer.Write(Sample);
                        _dataChunkSize += 4;
                    }
                    else throw new InvalidOperationException("Only 16, 24 or 32 bit PCM or IEEE float audio data supported");
                    break;
            }
        }

        /// <summary>
        /// Writes 32 bit floating point samples to the Wave file
        /// They will be converted to the appropriate bit depth depending on the WaveFormat of the WAV file
        /// </summary>
        /// <param name="samples">The buffer containing the floating point samples</param>
        /// <param name="offset">The offset from which to start writing</param>
        /// <param name="count">The number of floating point samples to write</param>
        public void WriteSamples(float[] samples, int offset, int count)
        {
            for (var n = 0; n < count; n++)
                WriteSample(samples[offset + n]);
        }

        /// <summary>
        /// Writes 16 bit samples to the Wave file
        /// </summary>
        /// <param name="samples">The buffer containing the 16 bit samples</param>
        /// <param name="offset">The offset from which to start writing</param>
        /// <param name="count">The number of 16 bit samples to write</param>
        public void WriteSamples(short[] samples, int offset, int count)
        {
            // 16 bit PCM data
            switch (WaveFormat.BitsPerSample)
            {
                case 16:
                    for (var sample = 0; sample < count; sample++)
                        _writer.Write(samples[sample + offset]);
                    _dataChunkSize += count * 2;
                    break;

                case 24:

                    for (var sample = 0; sample < count; sample++)
                    {
                        var value = BitConverter.GetBytes(ushort.MaxValue * samples[sample + offset]);
                        _value24[0] = value[1];
                        _value24[1] = value[2];
                        _value24[2] = value[3];
                        _writer.Write(_value24);
                    }
                    _dataChunkSize += count * 3;
                    break;

                default:
                    if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Extensible)
                    {
                        for (var sample = 0; sample < count; sample++)
                            _writer.Write(ushort.MaxValue * samples[sample + offset]);
                        _dataChunkSize += count * 4;
                    }
                    // IEEE float data
                    else if (WaveFormat.BitsPerSample == 32 && WaveFormat.Encoding == WaveFormatEncoding.Float)
                    {
                        for (var sample = 0; sample < count; sample++)
                            _writer.Write(samples[sample + offset] / (float)(short.MaxValue + 1));
                        _dataChunkSize += count * 4;
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
            if (_outStream != null)
            {
                try { UpdateHeader(_writer); }
                finally
                {
                    // in a finally block as we don't want the FileStream to run its disposer in
                    // the GC thread if the code above caused an IOException (e.g. due to disk full)
                    _outStream.Close(); // will close the underlying base stream
                    _outStream = null;
                }
            }
            _writer?.Dispose();
        }

        /// <summary>
        /// Updates the header with file size information
        /// </summary>
        void UpdateHeader(BinaryWriter writer)
        {
            writer.Flush();

            // UpdateRiffChunk
            writer.Seek(4, SeekOrigin.Begin);
            writer.Write((uint)(_outStream.Length - 8));

            // UpdateFactChunk
            if (HasFactChunk)
            {
                var bitsPerSample = WaveFormat.BitsPerSample * WaveFormat.Channels;
                if (bitsPerSample != 0)
                {
                    writer.Seek((int)_factSampleCountPos, SeekOrigin.Begin);

                    writer.Write((int)(_dataChunkSize * 8 / bitsPerSample));
                }
            }

            // UpdateDataChunk
            writer.Seek((int)_dataSizePos, SeekOrigin.Begin);
            writer.Write((uint)_dataChunkSize);
        }

        /// <summary>
        /// Finaliser - should only be called if the user forgot to close this WaveFileWriter
        /// </summary>
        ~WaveFileWriter() { DoDispose(); }
    }
}

using System.IO;

namespace Screna.Audio
{
    /// <summary>
    /// Writes an audio file encoded using an <see cref="IAudioEncoder"/>.
    /// </summary>
    public class EncodedAudioFileWriter : IAudioFileWriter
    {
        readonly object _syncLock = new object();
        readonly BinaryWriter _writer;

        readonly IAudioEncoder _encoder;
        byte[] _encodedBuffer;

        /// <summary>
        /// Creates a new <see cref="EncodedAudioFileWriter"/>.
        /// </summary>
        /// <param name="OutStream">The <see cref="Stream"/> to write to.</param>
        /// <param name="Encoder">The <see cref="IAudioEncoder"/> to use to encode the audio.</param>
        public EncodedAudioFileWriter(Stream OutStream, IAudioEncoder Encoder)
        {
            _writer = new BinaryWriter(OutStream);

            _encoder = Encoder;

            Encoder.WaveFormat.Serialize(_writer);
        }

        /// <summary>
        /// Creates a new <see cref="EncodedAudioFileWriter"/>.
        /// </summary>
        /// <param name="FilePath">The path to file to write to.</param>
        /// <param name="Encoder">The <see cref="IAudioEncoder"/> to use to encode the audio.</param>
        public EncodedAudioFileWriter(string FilePath, IAudioEncoder Encoder)
            : this(new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read), Encoder) { }
        
        /// <summary>
        /// Encodes and writes a block of audio data.
        /// </summary>
        public void Write(byte[] Data, int Offset, int Count)
        {
            // Prevent accessing encoded buffer by multiple threads simultaneously
            lock (_syncLock)
            {
                _encoder.EnsureBufferIsSufficient(ref _encodedBuffer, Count);

                var encodedLength = _encoder.Encode(Data, Offset, Count, _encodedBuffer, 0);

                if (encodedLength > 0)
                    _writer.Write(_encodedBuffer, 0, encodedLength);
            }
        }

        /// <summary>
        /// Writes all buffered data to file.
        /// </summary>
        public void Flush()
        {
            lock (_syncLock)
            {
                // Flush the encoder
                _encoder.EnsureBufferIsSufficient(ref _encodedBuffer, 0);

                var encodedLength = _encoder.Flush(_encodedBuffer, 0);

                if (encodedLength > 0)
                    _writer.Write(_encodedBuffer, 0, encodedLength);
            }
        }

        /// <summary>
        /// Releases all resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Flush();

            _writer.Dispose();
            _encoder.Dispose();
        }
    }
}

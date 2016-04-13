using Screna.Audio;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Screna.Avi
{
    /// <summary>
    /// Writes an AVI file.
    /// </summary>
    public class AviWriter : IVideoFileWriter
    {
        #region Fields
        AviInternalWriter _writer;
        IAviVideoStream _videoStream;
        IAviAudioStream _audioStream;
        IAudioProvider _audioFacade;
        readonly byte[] _videoBuffer;

        /// <summary>
        /// Video Frame Rate.
        /// </summary>
        public int FrameRate { get; }
        
        /// <summary>
        /// Gets whether Audio is recorded.
        /// </summary>
        public bool SupportsAudio => _audioStream != null;
        #endregion

        public AviWriter(string FileName,
            IImageProvider ImageProvider,
            AviCodec Codec,
            int Quality = 70,
            int FrameRate = 10,
            IAudioProvider AudioFacade = null,
            IAudioEncoder AudioEncoder = null)
        {
            this.FrameRate = FrameRate;
            _audioFacade = AudioFacade;

            _writer = new AviInternalWriter(FileName)
            {
                FramesPerSecond = FrameRate,
                EmitIndex1 = true
            };

            CreateVideoStream(ImageProvider.Width, ImageProvider.Height, Quality, Codec);

            if (AudioFacade != null)
                CreateAudioStream(AudioFacade, AudioEncoder);

            _videoBuffer = new byte[ImageProvider.Width * ImageProvider.Height * 4];
        }

        /// <summary>
        /// Asynchronously writes an Image frame.
        /// </summary>
        /// <param name="Image">The Image frame to write.</param>
        /// <returns>The Task Object.</returns>
        public Task WriteFrameAsync(Bitmap Image)
        {
            var bits = Image.LockBits(new Rectangle(Point.Empty, Image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            Marshal.Copy(bits.Scan0, _videoBuffer, 0, _videoBuffer.Length);
            Image.UnlockBits(bits);

            return _videoStream.WriteFrameAsync(true, _videoBuffer, 0, _videoBuffer.Length);
        }

        #region Private Methods
        void CreateVideoStream(int Width, int Height, int Quality, AviCodec Codec)
        {
            // Select encoder type based on FOURCC of codec
            if (Codec == AviCodec.Uncompressed)
                _videoStream = _writer.AddUncompressedVideoStream(Width, Height);
            else if (Codec == AviCodec.MotionJpeg)
                _videoStream = _writer.AddMotionJpegVideoStream(Width, Height, Quality);
            else
            {
                _videoStream = _writer.AddMpeg4VideoStream(Width, Height,
                    (double)_writer.FramesPerSecond,
                    // It seems that all tested MPEG-4 VfW codecs ignore the quality affecting parameters passed through VfW API
                    // They only respect the settings from their own configuration dialogs, and Mpeg4VideoEncoder currently has no support for this
                    Quality: Quality,
                    Codec: Codec,
                    // Most of VfW codecs expect single-threaded use, so we wrap this encoder to special wrapper
                    // Thus all calls to the encoder (including its instantiation) will be invoked on a single thread although encoding (and writing) is performed asynchronously
                    ForceSingleThreadedAccess: true);
            }

            _videoStream.Name = "ScrenaVideo";
        }

        void CreateAudioStream(IAudioProvider AudioFacade, IAudioEncoder AudioEncoder)
        {
            var wf = AudioFacade.WaveFormat;

            // Create encoding or simple stream based on settings
            _audioStream = AudioEncoder != null 
                ? _writer.AddEncodingAudioStream(AudioEncoder)
                : _writer.AddAudioStream(wf);

            _audioStream.Name = "ScrenaAudio";
        }
        #endregion

        /// <summary>
        /// Enumerates all available Avi Encoders.
        /// </summary>
        public static IEnumerable<AviCodec> EnumerateEncoders()
        {
            yield return AviCodec.Uncompressed;
            yield return AviCodec.MotionJpeg;
            foreach (var codec in Mpeg4VideoEncoderVcm.GetAvailableCodecs())
                yield return codec;
        }

        /// <summary>
        /// Write audio block to Audio Stream.
        /// </summary>
        /// <param name="Buffer">Buffer containing audio data.</param>
        /// <param name="Length">Length of audio data in bytes.</param>
        public void WriteAudio(byte[] Buffer, int Length) => _audioStream?.WriteBlock(Buffer, 0, Length);

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
        public void Dispose()
        {
            _writer.Close();
            _writer = null;

            if (_audioFacade == null)
                return;

            _audioFacade.Dispose();
            _audioFacade = null;
        }
    }
}

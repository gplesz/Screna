using Screna.Audio;
using System;

namespace Screna.Avi
{
    /// <summary>
    /// Provides extension methods for creating encoding streams with specific encoders.
    /// </summary>
    static class EncodingStreamFactory
    {
        /// <summary>
        /// Adds new video stream with <see cref="UncompressedVideoEncoder"/>.
        /// </summary>
        /// <seealso cref="AviInternalWriter.AddEncodingVideoStream"/>
        /// <seealso cref="UncompressedVideoEncoder"/>
        public static IAviVideoStream AddUncompressedVideoStream(this AviInternalWriter Writer,
                                                                 int Width,
                                                                 int Height)
        {
            var encoder = new UncompressedVideoEncoder(Width, Height);
            return Writer.AddEncodingVideoStream(encoder, true, Width, Height);
        }

        /// <summary>
        /// Adds new video stream with <see cref="MotionJpegVideoEncoderWpf"/>.
        /// </summary>
        /// <param name="Writer">Writer object to which new stream is added.</param>
        /// <param name="Width">Frame width.</param>
        /// <param name="Height">Frame height.</param>
        /// <param name="Quality">Requested quality of compression.</param>
        /// <seealso cref="AviInternalWriter.AddEncodingVideoStream"/>
        /// <seealso cref="MotionJpegVideoEncoderWpf"/>
        public static IAviVideoStream AddMotionJpegVideoStream(this AviInternalWriter Writer, 
                                                               int Width, 
                                                               int Height,
                                                               int Quality = 70)
        {
            var encoder = new MotionJpegVideoEncoderWpf(Width, Height, Quality);
            return Writer.AddEncodingVideoStream(encoder, true, Width, Height);
        }

        /// <summary>
        /// Adds new video stream with <see cref="Mpeg4VideoEncoderVcm"/>.
        /// </summary>
        /// <param name="Writer">Writer object to which new stream is added.</param>
        /// <param name="Width">Frame width.</param>
        /// <param name="Height">Frame height.</param>
        /// <param name="Fps">Frames rate of the video.</param>
        /// <param name="FrameCount">Number of frames if known in advance. Otherwise, specify <c>0</c>.</param>
        /// <param name="Quality">Requested quality of compression.</param>
        /// <param name="Codec">Specific MPEG-4 codec to use.</param>
        /// <param name="ForceSingleThreadedAccess">
        /// When <c>true</c>, the created <see cref="Mpeg4VideoEncoderVcm"/> instance is wrapped into
        /// <see cref="SingleThreadedVideoEncoderWrapper"/>.
        /// </param>
        /// <seealso cref="AviInternalWriter.AddEncodingVideoStream"/>
        /// <seealso cref="Mpeg4VideoEncoderVcm"/>
        /// <seealso cref="SingleThreadedVideoEncoderWrapper"/>
        public static IAviVideoStream AddMpeg4VideoStream(this AviInternalWriter Writer,
                                                            int Width,
                                                            int Height, 
                                                            double Fps,
                                                            int FrameCount = 0,
                                                            int Quality = 70,
                                                            AviCodec Codec = null, 
                                                            bool ForceSingleThreadedAccess = false)
        {
            var encoderFactory = Codec != null
                ? (() => new Mpeg4VideoEncoderVcm(Width, Height, Fps, FrameCount, Quality, Codec.FourCC))
                : new Func<IVideoEncoder>(() => new Mpeg4VideoEncoderVcm(Width, Height, Fps, FrameCount, Quality));
            var encoder = ForceSingleThreadedAccess
                ? new SingleThreadedVideoEncoderWrapper(encoderFactory)
                : encoderFactory.Invoke();
            return Writer.AddEncodingVideoStream(encoder, true, Width, Height);
        }

        /// <summary>
        /// Adds new audio stream with <see cref="Mp3EncoderLame"/>.
        /// </summary>
        /// <seealso cref="AviInternalWriter.AddEncodingAudioStream"/>
        /// <seealso cref="Mp3EncoderLame"/>
        public static IAviAudioStream AddMp3AudioStream(this AviInternalWriter Writer,
                                                        int ChannelCount = 2,
                                                        int SampleRate = 44100,
                                                        int OutputBitRateKbps = 160)
        {
            var encoder = new Mp3EncoderLame(ChannelCount, SampleRate, OutputBitRateKbps);
            return Writer.AddEncodingAudioStream(encoder);
        }
    }
}

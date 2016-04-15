using System;
using System.Drawing;
using System.Threading.Tasks;
using Screna.Audio;

namespace Screna
{
    /// <summary>
    /// Creates a video from individual frames and writes them to a file.
    /// </summary>
    public interface IVideoFileWriter : IDisposable
    {
        /// <summary>
        /// Initialises the <see cref="IVideoFileWriter"/>. Usually called by an <see cref="IRecorder"/>.
        /// </summary>
        /// <param name="ImageProvider">The Image Provider.</param>
        /// <param name="FrameRate">Video Frame Rate.</param>
        /// <param name="AudioProvider">The Audio Provider.</param>
        void Init(IImageProvider ImageProvider, int FrameRate, IAudioProvider AudioProvider);

        /// <summary>
        /// Asynchronously writes an Image frame.
        /// </summary>
        /// <param name="Image">The Image frame to write.</param>
        /// <returns>The Task Object.</returns>
        Task WriteFrameAsync(Bitmap Image);

        /// <summary>
        /// Gets whether audio is supported.
        /// </summary>
        bool SupportsAudio { get; }

        /// <summary>
        /// Video Frame Rate.
        /// </summary>
        int FrameRate { get; }
        
        /// <summary>
        /// Write audio block to Audio Stream.
        /// </summary>
        /// <param name="Buffer">Buffer containing audio data.</param>
        /// <param name="Length">Length of audio data in bytes.</param>
        void WriteAudio(byte[] Buffer, int Length);
    }
}

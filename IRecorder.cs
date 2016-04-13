using System;

namespace Screna
{
    /// <summary>
    /// Carries out the process of recording Audio and/or Video.
    /// </summary>
    public interface IRecorder
    {
        /// <summary>
        /// Fired when Recording Stops.
        /// </summary>
        event Action<Exception> RecordingStopped;

        /// <summary>
        /// Start Recording.
        /// </summary>
        /// <param name="Delay">Delay before recording starts.</param>
        void Start(int Delay = 0);

        /// <summary>
        /// Stop Recording and Perform Cleanup.
        /// </summary>
        void Stop();

        /// <summary>
        /// Pause Recording.
        /// </summary>
        void Pause();
    }
}

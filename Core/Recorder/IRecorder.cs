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
        event EventHandler<EndEventArgs> RecordingStopped;

        /// <summary>
        /// Gets the State of the Recorder.
        /// </summary>
        RecorderState State { get; }

        /// <summary>
        /// Start Recording.
        /// </summary>
        /// <param name="Delay">Delay (in milliseconds) before recording starts... 0 (Default) = Start immediately.</param>
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

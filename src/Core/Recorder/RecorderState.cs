namespace Screna
{
    /// <summary>
    /// Represents the State of a <see cref="IRecorder"/>.
    /// </summary>
    public enum RecorderState
    {
        /// <summary>
        /// Ready for Recording to be started.
        /// </summary>
        Ready,
        
        /// <summary>
        /// Currently Recording.
        /// </summary>
        Recording,

        /// <summary>
        /// Recording Paused.
        /// </summary>
        Paused,

        /// <summary>
        /// Recording Stopped
        /// </summary>
        Stopped
    }
}
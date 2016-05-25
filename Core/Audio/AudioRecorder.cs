namespace Screna.Audio
{
    /// <summary>
    /// An <see cref="IRecorder"/> for recording only Audio.
    /// </summary>
    public class AudioRecorder : RecorderBase
    {
        readonly IAudioFileWriter _writer;
        readonly IAudioProvider _audioProvider;

        /// <summary>
        /// Creates a new instance of <see cref="AudioRecorder"/>.
        /// </summary>
        /// <param name="Provider">The Audio Source.</param>
        /// <param name="Writer">The <see cref="IAudioFileWriter"/> to write audio to.</param>
        public AudioRecorder(IAudioProvider Provider, IAudioFileWriter Writer)
        {
            _audioProvider = Provider;
            _writer = Writer;
            
            _audioProvider.DataAvailable += (s, e) => _writer.Write(e.Buffer, 0, e.Length);
            _audioProvider.RecordingStopped += (s, e) => RaiseRecordingStopped(e.Error);
        }

        /// <summary>
        /// Override this method with the code to start recording.
        /// </summary>
        protected override void OnStart() => _audioProvider.Start();

        /// <summary>
        /// Override this method with the code to stop recording.
        /// </summary>
        protected override void OnStop()
        {
            _audioProvider?.Dispose();
            _writer?.Dispose();
        }

        /// <summary>
        /// Override this method with the code to pause recording.
        /// </summary>
        protected override void OnPause() => _audioProvider.Stop();
    }
}

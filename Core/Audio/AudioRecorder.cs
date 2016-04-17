using System;
using System.Threading;

namespace Screna.Audio
{
    /// <summary>
    /// An <see cref="IRecorder"/> for recording only Audio.
    /// </summary>
    public class AudioRecorder : IRecorder
    {
        IAudioFileWriter _writer;
        readonly IAudioProvider _audioProvider;
        readonly SynchronizationContext _syncContext;

        /// <summary>
        /// Creates a new instance of <see cref="AudioRecorder"/>.
        /// </summary>
        /// <param name="Provider">The Audio Source.</param>
        /// <param name="Writer">The <see cref="IAudioFileWriter"/> to write audio to.</param>
        public AudioRecorder(IAudioProvider Provider, IAudioFileWriter Writer)
        {
            _audioProvider = Provider;
            _writer = Writer;

            _syncContext = SynchronizationContext.Current;

            _audioProvider.DataAvailable += (s, e) => _writer.Write(e.Buffer, 0, e.Length);
            _audioProvider.RecordingStopped += (s, e) =>
            {
                var handler = RecordingStopped;

                if (handler == null)
                    return;

                if (_syncContext != null)
                    _syncContext.Post(S => handler(this, e), null);

                else handler(this, e);
            };
        }

        /// <summary>
        /// Start Recording.
        /// </summary>
        /// <param name="Delay">Delay before starting capture.</param>
        public void Start(int Delay = 0)
        {
            new Thread(() =>
            {
                try
                {
                    Thread.Sleep(Delay);
                    
                    _audioProvider.Start();
                }
                catch { }
            }).Start();
        }

        /// <summary>
        /// Stop Recording.
        /// </summary>
        public void Stop()
        {
            _audioProvider?.Dispose();
            _writer?.Dispose();
        }

        /// <summary>
        /// Pause Recording.
        /// </summary>
        public void Pause() => _audioProvider.Stop();

        /// <summary>
        /// Raised when Recording stops.
        /// </summary>
        public event EventHandler<EndEventArgs> RecordingStopped;
    }
}

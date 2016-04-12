using System;
using System.Threading;

namespace Screna.Audio
{
    public class AudioRecorder : IRecorder
    {
        IAudioFileWriter _writer;
        readonly IAudioProvider _audioProvider;
        readonly SynchronizationContext _syncContext;

        public AudioRecorder(IAudioProvider provider, IAudioFileWriter writer)
        {
            _audioProvider = provider;
            _writer = writer;

            _syncContext = SynchronizationContext.Current;

            _audioProvider.DataAvailable += (data, length) => _writer.Write(data, 0, length);
            _audioProvider.RecordingStopped += e =>
            {
                var handler = RecordingStopped;

                if (handler == null)
                    return;

                if (_syncContext != null)
                    _syncContext.Post(s => handler(e), null);

                else handler(e);
            };
        }

        public void Start(int Delay = 0) => _audioProvider.Start();

        public void Stop()
        {
            _audioProvider?.Dispose();

            if (_writer == null)
                return;

            _writer.Dispose();
            _writer = null;
        }

        public void Pause() => _audioProvider.Stop();

        public event Action<Exception> RecordingStopped;
    }
}

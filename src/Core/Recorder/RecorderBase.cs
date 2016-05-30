using System;
using System.Threading;
using System.Threading.Tasks;

namespace Screna
{
    /// <summary>
    /// Base implementation for <see cref="IRecorder"/> interface.
    /// </summary>
    public abstract class RecorderBase : IRecorder
    {
        readonly SynchronizationContext _syncContext;
        
        /// <summary>
        /// Init <see cref="RecorderBase"/>.
        /// </summary>
        protected RecorderBase()
        {
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Fired when Recording Stops.
        /// </summary>
        public event EventHandler<EndEventArgs> RecordingStopped;
        
        /// <summary>
        /// Raise <see cref="RecordingStopped"/> event taking care of <see cref="SynchronizationContext"/>.
        /// </summary>
        /// <param name="E">Exception, if occured else null.</param>
        protected void RaiseRecordingStopped(Exception E)
        {
            var handler = RecordingStopped;

            if (handler == null)
                return;

            if (_syncContext != null)
                _syncContext.Post(S => handler(this, new EndEventArgs(E)), null);

            else handler(this, new EndEventArgs(E));

            State = RecorderState.Stopped;
        }

        /// <summary>
        /// Gets the State of the Recorder.
        /// </summary>
        public RecorderState State { get; private set; } = RecorderState.Ready;

        /// <summary>
        /// Start Recording.
        /// </summary>
        /// <param name="Delay">Delay (in milliseconds) before recording starts... 0 (Default) = Start immediately.</param>
        public async void Start(int Delay = 0)
        {
            if (State == RecorderState.Stopped)
                return;

            try
            {
                await Task.Delay(Delay);

                OnStart();

                State = RecorderState.Recording;
            }
            catch (Exception e) { RaiseRecordingStopped(e); }
        }

        /// <summary>
        /// Override this method with the code to start recording.
        /// </summary>
        protected abstract void OnStart();

        /// <summary>
        /// Stop Recording and Perform Cleanup.
        /// </summary>
        public void Stop()
        {
            if (State == RecorderState.Stopped)
                return;

            OnStop();

            State = RecorderState.Stopped;
        }

        /// <summary>
        /// Override this method with the code to stop recording.
        /// </summary>
        protected abstract void OnStop();

        /// <summary>
        /// Pause Recording.
        /// </summary>
        public void Pause()
        {
            if (State == RecorderState.Paused)
                return;
            
            OnPause();

            State = RecorderState.Paused;
        }
        
        /// <summary>
        /// Override this method with the code to pause recording.
        /// </summary>
        protected abstract void OnPause();
    }
}
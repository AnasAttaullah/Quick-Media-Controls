using System;
using System.Diagnostics;
using Windows.Media.Control;

namespace Quick_Media_Controls.Services.SessionChangeDetector
{
    /// <summary>
    /// Windows 11+ session change detector using native events.
    /// </summary>
    internal class EventBasedSessionChangeDetector : ISessionChangeDetector
    {
        private readonly GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private readonly Action<GlobalSystemMediaTransportControlsSession?> _onSessionChanged;
        private bool _isDisposed;

        public EventBasedSessionChangeDetector(
            GlobalSystemMediaTransportControlsSessionManager sessionManager,
            Action<GlobalSystemMediaTransportControlsSession?> onSessionChanged)
        {
            _sessionManager = sessionManager;
            _onSessionChanged = onSessionChanged;
        }

        public void Start()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(EventBasedSessionChangeDetector));

            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
        }

        private void OnCurrentSessionChanged(
            GlobalSystemMediaTransportControlsSessionManager sender,
            CurrentSessionChangedEventArgs args)
        {
            try
            {
                var newSession = sender.GetCurrentSession();
                _onSessionChanged.Invoke(newSession);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in event-based session change: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
    }
}

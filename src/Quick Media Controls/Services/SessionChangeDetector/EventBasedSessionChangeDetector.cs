using System;
using System.Diagnostics;
using Windows.Media.Control;

namespace Quick_Media_Controls.Services.SessionChangeDetector
{
    /// <summary>
    /// Windows 11+ session change detector using native events
    /// </summary>

    internal class EventBasedSessionChangeDetector : ISessionChangeDetector
    {
        private readonly GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private readonly Action<GlobalSystemMediaTransportControlsSession?> _onSessionChanged;

        public EventBasedSessionChangeDetector(GlobalSystemMediaTransportControlsSessionManager sessionManager, Action<GlobalSystemMediaTransportControlsSession?> onSessionChanged)
        {
            _sessionManager = sessionManager;
            _onSessionChanged = onSessionChanged;
        }

        public void Start()
        {
            _sessionManager.SessionsChanged += OnSessionsChanged;
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            try
            {
                var newSession = _sessionManager.GetCurrentSession();
                _onSessionChanged.Invoke(newSession);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in event-based session change: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _sessionManager.SessionsChanged -= OnSessionsChanged;
        }
    }
}

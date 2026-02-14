using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Media_Control_Tray_Icon.Services
{
    public class MediaSessionService : IDisposable
    {
        public GlobalSystemMediaTransportControlsSessionManager? SessionManager { get; private set; }
        public GlobalSystemMediaTransportControlsSession? CurrentSession { get; private set; }
        public GlobalSystemMediaTransportControlsSessionPlaybackInfo? CurrentPlaybackInfo { get; private set; }

        public event EventHandler<GlobalSystemMediaTransportControlsSessionManager>? SessionChanged;
        public event EventHandler<GlobalSystemMediaTransportControlsSessionPlaybackInfo>? PlaybackInfoChanged;
        public event EventHandler? MediaPropertiesChanged;

        private ISessionChangeDetector? _sessionChangeDetector;
        private string? _lastSessionId;
        
        // Methods
        public async Task InitializeAsync()
        {
            SessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); 
            CurrentSession = SessionManager.GetCurrentSession();
            
            if (CurrentSession != null)
            {
                _lastSessionId = CurrentSession.SourceAppUserModelId;
                CurrentPlaybackInfo = CurrentSession.GetPlaybackInfo();
                CurrentSession.PlaybackInfoChanged += OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged += OnCurrentSession_MediaPropertiesChanged;
            }

            // Select appropriate session change detection strategy based on OS
            var osVersion = Environment.OSVersion;
            var isWindows10 = osVersion.Version.Major == 10 && osVersion.Version.Build < 22000;
            
            if (isWindows10)
            {
                System.Diagnostics.Debug.WriteLine("Windows 10 detected: Using polling strategy");
                _sessionChangeDetector = new PollingSessionChangeDetector(SessionManager, OnSessionChangeDetected);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Windows 11+ detected: Using event-based strategy");
                _sessionChangeDetector = new EventBasedSessionChangeDetector(SessionManager, OnSessionChangeDetected);
            }

            _sessionChangeDetector.Start();
        }

        private void OnSessionChangeDetected(GlobalSystemMediaTransportControlsSession? newSession)
        {
            var newSessionId = newSession?.SourceAppUserModelId;
            
            if (newSessionId != _lastSessionId)
            {
                System.Diagnostics.Debug.WriteLine($"Session change: {_lastSessionId} -> {newSessionId}");
                UpdateCurrentSession(newSession);
                _lastSessionId = newSessionId;
            }
        }

        private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? newSession)
        {
            // Clean up old session event handlers
            if(CurrentSession != null)
            {
                CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
            }

            CurrentSession = newSession;

            if (CurrentSession != null)
            {
                CurrentPlaybackInfo = CurrentSession.GetPlaybackInfo();
                CurrentSession.PlaybackInfoChanged += OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged += OnCurrentSession_MediaPropertiesChanged;
            }
            else
            {
                CurrentPlaybackInfo = null;
            }

            SessionChanged?.Invoke(this, SessionManager);
        }

        public async Task TogglePlayPauseAsync()
        {
            if(CurrentSession != null)
            {
                await CurrentSession.TryTogglePlayPauseAsync();
            }
        }

        public async Task SkipNextAsync()
        {
            if(CurrentSession != null)
            {
                await CurrentSession.TrySkipNextAsync();
            }
        }

        public async Task SkipPreviousAsync()
        {
            if(CurrentSession != null)
            {
                await CurrentSession.TrySkipPreviousAsync();
            }
        }

        public bool IsPlaying()
        {
            return CurrentPlaybackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }

        // Event Handlers   
        private void OnCurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            MediaPropertiesChanged?.Invoke(this, EventArgs.Empty); 
        }

        private void OnCurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            CurrentPlaybackInfo = CurrentSession?.GetPlaybackInfo();
            if(CurrentPlaybackInfo != null)
            {
                PlaybackInfoChanged?.Invoke(this, CurrentPlaybackInfo);
            }
        }

        // On Dispose
        public void Dispose()
        {
            _sessionChangeDetector?.Dispose();
            
            if(CurrentSession != null)
            {
                CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
            }
        }
    }

    // Strategy interface
    internal interface ISessionChangeDetector : IDisposable
    {
        void Start();
    }

    // Windows 11 implementation
    internal class EventBasedSessionChangeDetector : ISessionChangeDetector
    {
        private readonly GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private readonly Action<GlobalSystemMediaTransportControlsSession?> _onSessionChanged;

        public EventBasedSessionChangeDetector(
            GlobalSystemMediaTransportControlsSessionManager sessionManager,
            Action<GlobalSystemMediaTransportControlsSession?> onSessionChanged)
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
                var newSession = sender?.GetCurrentSession();
                _onSessionChanged?.Invoke(newSession);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in event-based session change: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _sessionManager.SessionsChanged -= OnSessionsChanged;
        }
    }

    // Windows 10 implementation
    internal class PollingSessionChangeDetector : ISessionChangeDetector
    {
        private readonly GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private readonly Action<GlobalSystemMediaTransportControlsSession?> _onSessionChanged;
        private Timer? _pollTimer;

        public PollingSessionChangeDetector(
            GlobalSystemMediaTransportControlsSessionManager sessionManager,
            Action<GlobalSystemMediaTransportControlsSession?> onSessionChanged)
        {
            _sessionManager = sessionManager;
            _onSessionChanged = onSessionChanged;
        }

        public void Start()
        {
            _pollTimer = new Timer(CheckForSessionChange, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void CheckForSessionChange(object? state)
        {
            try
            {
                var newSession = _sessionManager?.GetCurrentSession();
                _onSessionChanged?.Invoke(newSession);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in polling session change: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
        }
    }
}

using Media_Control_Tray_Icon.Services.SessionChangeDetector;
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
        public GlobalSystemMediaTransportControlsSessionMediaProperties? CurrentMediaProperties { get; private set; }

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

            // Detecting OS
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
            if (CurrentSession != null)
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
                CurrentMediaProperties = null;
            }

            SessionChanged?.Invoke(this, SessionManager);
            MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task TogglePlayPauseAsync()
        {
            if (CurrentSession != null)
            {
                await CurrentSession.TryTogglePlayPauseAsync();
            }
        }

        public async Task SkipNextAsync()
        {
            if (CurrentSession != null)
            {
                await CurrentSession.TrySkipNextAsync();
            }
        }

        public async Task SkipPreviousAsync()
        {
            if (CurrentSession != null)
            {
                await CurrentSession.TrySkipPreviousAsync();
            }
        }

        public bool IsPlaying()
        {
            return CurrentPlaybackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }

        // Event Handlers   
        private async void OnCurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            if (CurrentSession != null)
            {
                CurrentMediaProperties = await CurrentSession.TryGetMediaPropertiesAsync();
            }
                MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnCurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            CurrentPlaybackInfo = CurrentSession?.GetPlaybackInfo();
            if (CurrentPlaybackInfo != null)
            {
                PlaybackInfoChanged?.Invoke(this, CurrentPlaybackInfo);
            }
        }

        // On Dispose
        public void Dispose()
        {
            _sessionChangeDetector?.Dispose();

            if (CurrentSession != null)
            {
                CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
            }
        }
    }

    }


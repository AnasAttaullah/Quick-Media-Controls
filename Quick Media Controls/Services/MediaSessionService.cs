using Quick_Media_Controls.Services.SessionChangeDetector;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Quick_Media_Controls.Services
{
    /// <summary>
    /// Manages Windows media session interactions and monitors session changes.
    /// </summary>
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
        private bool _isDisposed;

        public async Task InitializeAsync()
        {
            try
            {
                SessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                CurrentSession = SessionManager.GetCurrentSession();

                if (CurrentSession != null)
                {
                    _lastSessionId = CurrentSession.SourceAppUserModelId;
                    CurrentPlaybackInfo = CurrentSession.GetPlaybackInfo();
                    CurrentSession.PlaybackInfoChanged += OnCurrentSession_PlaybackInfoChanged;
                    CurrentSession.MediaPropertiesChanged += OnCurrentSession_MediaPropertiesChanged;
                    
                    CurrentMediaProperties = await CurrentSession.TryGetMediaPropertiesAsync();
                }

                var osVersion = Environment.OSVersion;
                var isWindows10 = osVersion.Version.Major == 10 && osVersion.Version.Build < 22000;

                if (isWindows10)
                {
                    System.Diagnostics.Debug.WriteLine("Windows 10 detected: Using polling strategy");
                    _sessionChangeDetector = new PollingSessionChangeDetector(SessionManager, OnSessionChangeDetectedAsync);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Windows 11+ detected: Using event-based strategy");
                    _sessionChangeDetector = new EventBasedSessionChangeDetector(SessionManager, OnSessionChangeDetectedAsync);
                }

                _sessionChangeDetector.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize MediaSessionService: {ex.Message}");
            }
        }

        private void OnSessionChangeDetectedAsync(GlobalSystemMediaTransportControlsSession? newSession)
        {
            if (_isDisposed) return;

            var newSessionId = newSession?.SourceAppUserModelId;
            if (newSessionId != _lastSessionId)
            {
                System.Diagnostics.Debug.WriteLine($"Session change: {_lastSessionId} -> {newSessionId}");
                UpdateCurrentSessionAsync(newSession);
                _lastSessionId = newSessionId;
            }
        }

        private async void UpdateCurrentSessionAsync(GlobalSystemMediaTransportControlsSession? newSession)
        {
            if (_isDisposed) return;

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

            await FetchMediaAsync();

            if (_isDisposed) return;

            SessionChanged?.Invoke(this, SessionManager);
            MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task TogglePlayPauseAsync()
        {
            try
            {
                if (CurrentSession == null) return;
                await CurrentSession.TryTogglePlayPauseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling playback: {ex.Message}");
            }
        }

        public async Task SkipNextAsync()
        {
            try
            {
                if (CurrentSession == null) return;
                await CurrentSession.TrySkipNextAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error skipping to next track: {ex.Message}");
            }
        }

        public async Task SkipPreviousAsync()
        {
            try
            {
                if (CurrentSession == null) return;
                await CurrentSession.TrySkipPreviousAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error skipping to previous track: {ex.Message}");
            }
        }

        public async Task FetchMediaAsync()
        {
            try
            {
                if (CurrentSession == null) return;
                CurrentMediaProperties = await CurrentSession.TryGetMediaPropertiesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching media properties: {ex.Message}");
            }
        }

        public bool IsPlaying()
        {
            return CurrentPlaybackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        public bool IsNextEnabled()
        {
            return CurrentPlaybackInfo?.Controls?.IsNextEnabled ?? false;
        }

        public bool IsPreviousEnabled()
        {
            return CurrentPlaybackInfo?.Controls?.IsPreviousEnabled ?? false;
        }

        public bool HasPlaylist()
        {
            return IsNextEnabled() || IsPreviousEnabled();
        }
 
        private async void OnCurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            try
            {
                if (CurrentSession != null)
                {
                    CurrentMediaProperties = await CurrentSession.TryGetMediaPropertiesAsync();
                }
                MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching media properties: {ex.Message}");
            }
        }

        private void OnCurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                CurrentPlaybackInfo = CurrentSession?.GetPlaybackInfo();
                if (CurrentPlaybackInfo != null)
                {
                    PlaybackInfoChanged?.Invoke(this, CurrentPlaybackInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating playback info: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _sessionChangeDetector?.Dispose();
            _sessionChangeDetector = null;

            if (CurrentSession != null)
            {
                CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
            }

            CurrentSession = null;
            CurrentPlaybackInfo = null;
            CurrentMediaProperties = null;

            SessionChanged = null;
            PlaybackInfoChanged = null;
            MediaPropertiesChanged = null;

            GC.SuppressFinalize(this);
        }
    }

    }


using Quick_Media_Controls.Services.SessionChangeDetector;
using System;
using System.Diagnostics;
using System.Threading;
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

        public event EventHandler<GlobalSystemMediaTransportControlsSessionManager?>? SessionChanged;
        public event EventHandler<GlobalSystemMediaTransportControlsSessionPlaybackInfo>? PlaybackInfoChanged;
        public event EventHandler? MediaPropertiesChanged;

        private ISessionChangeDetector? _sessionChangeDetector;
        private string? _lastSessionId;
        private int _lastSessionCount = 0;
        private bool _isDisposed;
        private readonly SemaphoreSlim _sessionChangeLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _unlockGracePeriodCts;
        private readonly object _gracePeriodLock = new object();

        public bool IsLocked { get; private set; }

        private bool IsSessionAlive(GlobalSystemMediaTransportControlsSessionPlaybackInfo? info, string? title)
        {
            // 1. If it has a title, it's definitively alive.
            if (!string.IsNullOrWhiteSpace(title)) return true;

            // 2. If it has no title, but controls are enabled, it's just buffering/transitioning.
            if (info?.Controls != null && 
               (info.Controls.IsPlayEnabled || info.Controls.IsPauseEnabled || info.Controls.IsNextEnabled || info.Controls.IsPreviousEnabled))
            {
                return true; 
            }

            // 3. Otherwise, it's a dead/ghost session.
            return false;
        }

        private void CancelUnlockGracePeriod()
        {
            lock (_gracePeriodLock)
            {
                if (_unlockGracePeriodCts != null)
                {
                    Debug.WriteLine($"Locked session {_lastSessionId} returned. Canceling auto-unlock grace period.");
                    _unlockGracePeriodCts.Cancel();
                }
            }
        }

        private void StartUnlockGracePeriod()
        {
            lock (_gracePeriodLock)
            {
                if (_unlockGracePeriodCts != null) return;

                Debug.WriteLine($"Locked session {_lastSessionId} appears dead. Starting 5s grace period...");
                _unlockGracePeriodCts = new CancellationTokenSource();
                var token = _unlockGracePeriodCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(5000, token);

                        if (!token.IsCancellationRequested)
                        {
                            await _sessionChangeLock.WaitAsync();
                            try
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    Debug.WriteLine($"Grace period expired for locked session {_lastSessionId}. Auto-unlocking...");
                                    IsLocked = false;

                                    var currentOSSession = SessionManager?.GetCurrentSession();
                                    _lastSessionId = currentOSSession?.SourceAppUserModelId;
                                    _ = UpdateCurrentSessionAsync(currentOSSession);
                                }
                            }
                            finally
                            {
                                _sessionChangeLock.Release();
                            }
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex) { Debug.WriteLine($"Error in grace period: {ex.Message}"); }
                    finally
                    {
                        lock (_gracePeriodLock)
                        {
                            _unlockGracePeriodCts?.Dispose();
                            _unlockGracePeriodCts = null;
                        }
                    }
                });
            }
        }

        public bool CanCycle
        {
            get
            {
                try
                {
                    var sessions = SessionManager?.GetSessions();
                    return sessions != null && sessions.Count > 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                SessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                CurrentSession = SessionManager.GetCurrentSession();

                var initialSessions = SessionManager.GetSessions();
                _lastSessionCount = initialSessions?.Count ?? 0;

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
                    Debug.WriteLine("Windows 10 detected: Using polling strategy");
                    _sessionChangeDetector = new PollingSessionChangeDetector(SessionManager, OnSessionChangeDetectedAsync);
                }
                else
                {
                    Debug.WriteLine("Windows 11+ detected: Using event-based strategy");
                    _sessionChangeDetector = new EventBasedSessionChangeDetector(SessionManager, OnSessionChangeDetectedAsync);
                }

                _sessionChangeDetector.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize MediaSessionService: {ex.Message}");
            }
        }

        private static async Task<GlobalSystemMediaTransportControlsSessionMediaProperties?> GetMediaPropertiesWithTimeoutAsync(GlobalSystemMediaTransportControlsSession? session, int timeoutMs = 500)
        {
            if (session == null) return null;
            try
            {
                using var cts = new CancellationTokenSource();
                var task = Task.Run(async () => await session.TryGetMediaPropertiesAsync());
                var delayTask = Task.Delay(timeoutMs, cts.Token);
                if (await Task.WhenAny(task, delayTask) == task)
                {
                    cts.Cancel();
                    return await task;
                }
                else
                {
                    Debug.WriteLine($"TryGetMediaPropertiesAsync timed out for {session.SourceAppUserModelId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting media properties: {ex.Message}");
                return null;
            }
        }

        private async void OnSessionChangeDetectedAsync(GlobalSystemMediaTransportControlsSession? newSession)
        {
            if (_isDisposed) return;

            bool shouldFireSessionChanged = false;
            GlobalSystemMediaTransportControlsSessionManager? sessionManagerSnapshot = null;

            await _sessionChangeLock.WaitAsync();
            try
            {
                var currentSessions = SessionManager?.GetSessions();
                int currentSessionCount = currentSessions?.Count ?? 0;
                
                if (currentSessionCount != _lastSessionCount)
                {
                    _lastSessionCount = currentSessionCount;
                    shouldFireSessionChanged = true;
                    sessionManagerSnapshot = SessionManager;
                }

                if (IsLocked)
                {
                    if (_lastSessionId == null)
                    {
                        IsLocked = false;
                    }
                    else
                    {
                        bool foundLockedSession = false;

                        if (currentSessions != null)
                        {
                            foreach (var s in currentSessions)
                            {
                                if (s.SourceAppUserModelId == _lastSessionId)
                                {
                                    var playbackInfo = s.GetPlaybackInfo();
                                    var props = await GetMediaPropertiesWithTimeoutAsync(s, 500);
                                    
                                    if (IsSessionAlive(playbackInfo, props?.Title))
                                    {
                                        foundLockedSession = true;
                                        if (props != null && props.Title != CurrentMediaProperties?.Title)
                                        {
                                            Debug.WriteLine($"Locked session updated: {_lastSessionId}");
                                            _ = UpdateCurrentSessionAsync(s);
                                        }
                                        break;
                                    }
                                }
                            }
                        }

                        if (!foundLockedSession)
                        {
                            StartUnlockGracePeriod();
                        }
                        else
                        {
                            CancelUnlockGracePeriod();
                        }
                    }

                    if (IsLocked) return;
                }

                if (newSession == null)
                {
                    if (CurrentSession != null)
                    {
                        _lastSessionId = null;
                        _ = UpdateCurrentSessionAsync(null);
                    }
                    return;
                }

                var newSessionId = newSession.SourceAppUserModelId;
                bool sessionChanged = false;

                if (newSessionId != _lastSessionId)
                {
                    sessionChanged = true;
                }
                else
                {
                    try
                    {
                        var newProps = await GetMediaPropertiesWithTimeoutAsync(newSession, 500);
                        if (newProps != null && newProps.Title != CurrentMediaProperties?.Title)
                        {
                            sessionChanged = true;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error comparing session properties: {ex.Message}"); }
                }

                if (sessionChanged)
                {
                    Debug.WriteLine($"Session change: {_lastSessionId} -> {newSessionId}");
                    _lastSessionId = newSessionId;
                    _ = UpdateCurrentSessionAsync(newSession);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in OnSessionChangeDetectedAsync: {ex.Message}");
            }
            finally
            {
                _sessionChangeLock.Release();
            }

            if (shouldFireSessionChanged)
            {
                SessionChanged?.Invoke(this, sessionManagerSnapshot);
            }
        }

        private async Task UpdateCurrentSessionAsync(GlobalSystemMediaTransportControlsSession? newSession)
        {
            if (_isDisposed) return;

            if (CurrentSession != null)
            {
                try
                {
                    CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                    CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to unsubscribe from old session: {ex.Message}");
                }
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
                CurrentMediaProperties = await GetMediaPropertiesWithTimeoutAsync(CurrentSession, 1000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching media properties: {ex.Message}");
            }
        }

        public async Task ToggleLockAsync()
        {
            IsLocked = !IsLocked;

            if (!IsLocked)
            {
                CancelUnlockGracePeriod();

                if (SessionManager != null)
                {
                    var currentOSSession = SessionManager.GetCurrentSession();
                    _lastSessionId = currentOSSession?.SourceAppUserModelId;
                    await UpdateCurrentSessionAsync(currentOSSession);
                }
            }
        }

        public async Task CycleSessionAsync()
        {
            if (SessionManager == null) return;

            await _sessionChangeLock.WaitAsync();
            try
            {
                var sessionsList = SessionManager.GetSessions();
                if (sessionsList == null || sessionsList.Count <= 1) return;

                string? targetSessionId = _lastSessionId;
                string? targetTitle = null;
                
                try
                {
                    targetTitle = CurrentMediaProperties?.Title;
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading current title: {ex.Message}"); }

                int currentIndex = -1;
                for (int i = 0; i < sessionsList.Count; i++)
                {
                    var s = sessionsList[i];
                    if (s.SourceAppUserModelId == targetSessionId)
                    {
                        try
                        {
                            var props = await GetMediaPropertiesWithTimeoutAsync(s, 500);
                            if (props != null && props.Title == targetTitle)
                            {
                                currentIndex = i;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error matching session during cycle: {ex.Message}");
                        }

                        if (currentIndex == -1)
                        {
                            currentIndex = i;
                        }
                    }
                }

                int nextIndex = currentIndex == -1 ? 0 : (currentIndex + 1) % sessionsList.Count;
                var nextSession = sessionsList[nextIndex];

                IsLocked = true; // Lock onto the manually selected session
                CancelUnlockGracePeriod();

                await UpdateCurrentSessionAsync(nextSession);
                _lastSessionId = nextSession.SourceAppUserModelId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cycling sessions: {ex.Message}");
            }
            finally
            {
                _sessionChangeLock.Release();
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

                    if (IsLocked)
                    {
                        if (!IsSessionAlive(CurrentPlaybackInfo, CurrentMediaProperties?.Title))
                        {
                            StartUnlockGracePeriod();
                        }
                        else
                        {
                            CancelUnlockGracePeriod();
                        }
                    }
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

                if (IsLocked)
                {
                    if (!IsSessionAlive(CurrentPlaybackInfo, CurrentMediaProperties?.Title))
                    {
                        StartUnlockGracePeriod();
                    }
                    else
                    {
                        CancelUnlockGracePeriod();
                    }
                }

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

            CancelUnlockGracePeriod();

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


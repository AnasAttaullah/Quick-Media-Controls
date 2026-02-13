using System;
using System.Security.Policy;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Media_Control_Tray_Icon.Services
{
    public class MediaSessionService : IDisposable
    {
        public GlobalSystemMediaTransportControlsSessionManager? SessionManager { get; private set; }
        public GlobalSystemMediaTransportControlsSession? CurrentSession { get; private set; }
        public GlobalSystemMediaTransportControlsSessionPlaybackInfo? CurrentPlaybackInfo { get; private set; }

        //public event EventHandler<GlobalSystemMediaTransportControlsSessionManager>? CurrentSessionChanged;
        public event EventHandler<GlobalSystemMediaTransportControlsSessionManager>? SessionChanged;
        public event EventHandler<GlobalSystemMediaTransportControlsSessionPlaybackInfo>? PlaybackInfoChanged;
        public event EventHandler? MediaPropertiesChanged;
        
        // Methods
        public async Task InitializeAsync()
        {
            SessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); 
            CurrentSession = SessionManager.GetCurrentSession();
            SessionManager.SessionsChanged += SessionManager_SessionsChanged;
            
            if (CurrentSession != null)
            {
                CurrentPlaybackInfo = CurrentSession.GetPlaybackInfo();
                CurrentSession.PlaybackInfoChanged += OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged += OnCurrentSession_MediaPropertiesChanged;
            }
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
            CurrentPlaybackInfo = CurrentSession.GetPlaybackInfo();
            if(CurrentPlaybackInfo != null)
            {
                PlaybackInfoChanged?.Invoke(this, CurrentPlaybackInfo);
            }
        }
        private void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            // Summary : Removes old event handlers, updates the current session and playback info, and attaches new event handlers to the new session.
            if(CurrentSession != null)
            {
                CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
                CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
            }

            CurrentSession = SessionManager.GetCurrentSession();

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

        // On Dispose
        public void Dispose()
        {
            if(SessionManager != null)
            {
                SessionManager.SessionsChanged -= SessionManager_SessionsChanged;
            }

           if(CurrentSession != null)
           {
               CurrentSession.PlaybackInfoChanged -= OnCurrentSession_PlaybackInfoChanged;
               CurrentSession.MediaPropertiesChanged -= OnCurrentSession_MediaPropertiesChanged;
           }
        }
    }
}

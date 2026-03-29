using System;

namespace Quick_Media_Controls.Services.SessionChangeDetector
{
    internal interface ISessionChangeDetector : IDisposable
    {
        void Start();
    }
}

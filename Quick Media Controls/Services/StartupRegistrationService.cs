using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel;

namespace Quick_Media_Controls.Services
{
    public sealed class StartupRegistrationService
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private readonly string _valueName;
        private readonly string _command;
        private readonly string _startupTaskId;
        private readonly AppDistributionService _appDistributionService;

        public StartupRegistrationService(
            string appName = "Quick Media Controls",
            string startupTaskId = "QuickMediaControlsStartupTask",
            AppDistributionService? appDistributionService = null)
        {
            _valueName = appName;
            _startupTaskId = startupTaskId;
            _command = BuildCommand();
            _appDistributionService = appDistributionService ?? new AppDistributionService();
        }

        public bool IsRegistered()
        {
            if (_appDistributionService.IsPackaged)
            {
                return IsPackagedStartupEnabled();
            }

            return IsRegistryStartupEnabled();
        }

        public void Apply(bool enabled)
        {
            if (_appDistributionService.IsPackaged)
            {
                ApplyPackagedStartup(enabled);
                return;
            }

            ApplyRegistryStartup(enabled);
        }

        private bool IsRegistryStartupEnabled()
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (runKey is null)
            {
                return false;
            }

            var currentValue = runKey.GetValue(_valueName) as string;
            if (string.Equals(currentValue, _command, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var name in runKey.GetValueNames())
            {
                if (runKey.GetValue(name) is string value &&
                    string.Equals(value, _command, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyRegistryStartup(bool enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Unable to open startup registry key.");

            if (enabled)
            {
                runKey.SetValue(_valueName, _command, RegistryValueKind.String);
                return;
            }

            if (runKey.GetValue(_valueName) != null)
            {
                runKey.DeleteValue(_valueName, throwOnMissingValue: false);
            }
        }

        private bool IsPackagedStartupEnabled()
        {
            var startupTask = GetStartupTaskOrNull();
            if (startupTask is null)
            {
                return false;
            }

            return startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }

        private void ApplyPackagedStartup(bool enabled)
        {
            var startupTask = GetStartupTaskOrNull()
                ?? throw new InvalidOperationException(
                    $"Startup task '{_startupTaskId}' is not available. Ensure it is declared in the package manifest.");

            if (enabled)
            {
                switch (startupTask.State)
                {
                    case StartupTaskState.Enabled:
                    case StartupTaskState.EnabledByPolicy:
                        return;

                    case StartupTaskState.DisabledByUser:
                        throw new InvalidOperationException(
                            "Startup is disabled by user in Windows Startup Apps settings.");

                    case StartupTaskState.DisabledByPolicy:
                        throw new InvalidOperationException(
                            "Startup is disabled by organization policy.");

                    case StartupTaskState.Disabled:
                    default:
                        var newState = startupTask.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                        if (newState is not StartupTaskState.Enabled and not StartupTaskState.EnabledByPolicy)
                        {
                            throw new InvalidOperationException(
                                $"Unable to enable startup task. Current state: {newState}.");
                        }

                        return;
                }
            }

            if (startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
            {
                startupTask.Disable();
            }
        }

        private StartupTask? GetStartupTaskOrNull()
        {
            try
            {
                return StartupTask.GetAsync(_startupTaskId).AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private static string BuildCommand()
        {
            var processPath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(processPath))
            {
                processPath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("Unable to resolve current executable path.");
            }

            return $"\"{Path.GetFullPath(processPath)}\"";
        }
    }
}
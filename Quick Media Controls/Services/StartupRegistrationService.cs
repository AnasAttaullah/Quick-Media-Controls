using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.EnterpriseData;

namespace Quick_Media_Controls.Services
{
    public sealed class StartupRegistrationService
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private readonly string _valueName;
        private readonly string _command;

        public StartupRegistrationService(string appName = "Quick Media Controls")
        {
            _valueName = appName;
            _command = BuildCommand();
        }

        public bool IsRegistered()
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var currentValue = runKey?.GetValue(_valueName) as string;

            return string.Equals(currentValue, _command, StringComparison.OrdinalIgnoreCase);
        }

        public void Apply(bool Enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Unable to open startup registry key.");

            if (Enabled)
            {
                runKey.SetValue(_valueName, _command,RegistryValueKind.String);
                return;
            }
            if (runKey.GetValue(_valueName) != null)
            {
                runKey.DeleteValue(_valueName, throwOnMissingValue: false);
            }
        }


        private static string? BuildCommand()
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

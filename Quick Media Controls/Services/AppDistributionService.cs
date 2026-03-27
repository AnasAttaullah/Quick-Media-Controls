using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Quick_Media_Controls.Services
{
    public enum AppDistributionChannel
    {
        Unpackaged,
        Packaged
    }
    public class AppDistributionService
    {
        private const int AppModelErrorNoPackage = 15700;
        private readonly Lazy<AppDistributionChannel> _distributionChannel;

        public AppDistributionService()
        {
            _distributionChannel = new Lazy<AppDistributionChannel>(DetectChannel);
        }

        public AppDistributionChannel DistributionChannel => _distributionChannel.Value;
        public bool IsPackaged => DistributionChannel == AppDistributionChannel.Packaged;

        private static AppDistributionChannel DetectChannel()
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            return result == AppModelErrorNoPackage ? AppDistributionChannel.Unpackaged : AppDistributionChannel.Packaged;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
    }
}

// AppVersion.cs — 集中管理版本号，修改后需同步 Git tag + Release
namespace VRCMicToggle
{
    /// <summary>
    /// Centralized application version information.
    /// To bump the version, change the Version constant here and create a matching Git tag (e.g. v1.0.1) + GitHub Release.
    /// </summary>
    internal static class AppVersion
    {
        public const string Version = "1.0.2";
    }
}

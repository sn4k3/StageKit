using StageKit.Runtime;
using StageKit.Updatum;

namespace StageKit.Demo;

public static class DemoUpdateManager
{
    public static UpdatumManager Create()
    {
        return new UpdatumManager("sn4k3", "UVtools")
        {
            AssetRegexPattern = $"^UVtools_{EntryApplication.GenericRuntimeIdentifier}_v",
            RequireAssetChecksum = true,
            AllowPreReleases = false,
            FetchOnlyLatestRelease = false,
            InstallUpdateWindowsInstallerArguments = "/qb",
            InstallUpdateCodesignMacOSApp = true,
            InstallUpdateSingleFileExecutableNameStrategy = UpdatumSingleFileExecutableNameStrategy.EntryApplicationName,
            InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer,
        };
    }
}
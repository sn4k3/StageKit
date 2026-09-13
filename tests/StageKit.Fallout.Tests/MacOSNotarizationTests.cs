using StageKit.Primitives.Extensions;
using System.Reflection;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using StageKit.Primitives;
using Xunit;
using ParameterAttribute = Fallout.Common.ParameterAttribute;

namespace StageKit.Fallout.Tests;

public class MacOSNotarizationTests
{
    [Fact]
    public void MacParameters_PublicSurface_HaveParameterAttributes()
    {
        var buildType = typeof(StageKitBuild);

        var signingIdentity = buildType.GetProperty(nameof(StageKitBuild.MacSigningIdentity));
        Assert.NotNull(signingIdentity);
        Assert.NotNull(signingIdentity!.GetCustomAttribute<ParameterAttribute>());

        var notarize = buildType.GetProperty(nameof(StageKitBuild.MacNotarize));
        Assert.NotNull(notarize);
        Assert.NotNull(notarize!.GetCustomAttribute<ParameterAttribute>());

        var keychainProfile = buildType.GetProperty(nameof(StageKitBuild.MacKeychainProfile));
        Assert.NotNull(keychainProfile);
        Assert.NotNull(keychainProfile!.GetCustomAttribute<ParameterAttribute>());

        var appleId = buildType.GetProperty(nameof(StageKitBuild.MacAppleId));
        Assert.NotNull(appleId);
        Assert.NotNull(appleId!.GetCustomAttribute<ParameterAttribute>());

        var password = buildType.GetProperty(nameof(StageKitBuild.MacPassword));
        Assert.NotNull(password);
        Assert.NotNull(password!.GetCustomAttribute<ParameterAttribute>());

        var teamId = buildType.GetProperty(nameof(StageKitBuild.MacTeamId));
        Assert.NotNull(teamId);
        Assert.NotNull(teamId!.GetCustomAttribute<ParameterAttribute>());

        var apiKeyPath = buildType.GetProperty(nameof(StageKitBuild.MacApiKeyPath));
        Assert.NotNull(apiKeyPath);
        Assert.NotNull(apiKeyPath!.GetCustomAttribute<ParameterAttribute>());

        var apiKeyId = buildType.GetProperty(nameof(StageKitBuild.MacApiKeyId));
        Assert.NotNull(apiKeyId);
        Assert.NotNull(apiKeyId!.GetCustomAttribute<ParameterAttribute>());

        var apiIssuerId = buildType.GetProperty(nameof(StageKitBuild.MacApiIssuerId));
        Assert.NotNull(apiIssuerId);
        Assert.NotNull(apiIssuerId!.GetCustomAttribute<ParameterAttribute>());
    }

    [Fact]
    public void HasMacNotarizationCredentials_Default_ReturnsFalse()
    {
        var build = new TestMacOSBuild();
        Assert.False(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_WithKeychainProfile_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.SetMacKeychainProfile("my-profile");
        Assert.True(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_WithAppleIdCredentials_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.SetMacAppleId("dev@example.com");
        build.SetMacPassword("app-password");
        build.SetMacTeamId("TEAM123456");
        Assert.True(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_WithPartialAppleId_ReturnsFalse()
    {
        var build = new TestMacOSBuild();
        build.SetMacAppleId("dev@example.com");
        build.SetMacPassword("app-password");
        // teamId missing
        Assert.False(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_WithApiKeyCredentials_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.SetMacApiKey((AbsolutePath)"/path/to/AuthKey.p8", "KEY123", "ISSUER456");
        Assert.True(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_WithPartialApiKey_ReturnsFalse()
    {
        var build = new TestMacOSBuild();
        build.SetMacApiKey((AbsolutePath)"/path/to/AuthKey.p8", "KEY123", null);
        Assert.False(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void HasMacNotarizationCredentials_FallbackToMacAppBundleOptions_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.TestMacAppBundleOptions.KeychainProfile = "options-keychain-profile";
        Assert.True(build.InvokeHasMacNotarizationCredentials());
    }

    [Fact]
    public void ShouldNotarizeMacOS_Default_ReturnsFalse()
    {
        var build = new TestMacOSBuild();
        Assert.False(build.InvokeShouldNotarizeMacOS());
    }

    [Fact]
    public void ShouldNotarizeMacOS_WhenMacNotarizeTrue_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.SetMacNotarize(true);
        Assert.True(build.InvokeShouldNotarizeMacOS());
    }

    [Fact]
    public void ShouldNotarizeMacOS_WhenCredentialsProvided_ReturnsTrue()
    {
        var build = new TestMacOSBuild();
        build.SetMacKeychainProfile("profile");
        Assert.True(build.InvokeShouldNotarizeMacOS());
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_WithKeychainProfile_ProducesExpectedArguments()
    {
        var build = new TestMacOSBuild();
        build.SetMacKeychainProfile("My Profile");
        var args = build.InvokeGetNotaryToolAuthenticationArguments();
        Assert.Equal("--keychain-profile \"My Profile\"", args);
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_WithAppleId_ProducesExpectedArguments()
    {
        var build = new TestMacOSBuild();
        build.SetMacAppleId("dev@example.com");
        build.SetMacPassword("app-password");
        build.SetMacTeamId("TEAM123456");
        var args = build.InvokeGetNotaryToolAuthenticationArguments();
        Assert.Equal("--apple-id \"dev@example.com\" --password \"app-password\" --team-id \"TEAM123456\"", args);
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_WithApiKey_ProducesExpectedArguments()
    {
        var build = new TestMacOSBuild();
        var keyPath = (AbsolutePath)"/keys/AuthKey_123.p8";
        build.SetMacApiKey(keyPath, "KEY123", "ISSUER456");
        var args = build.InvokeGetNotaryToolAuthenticationArguments();
        Assert.Equal($"--key {keyPath.ToString().QuoteProcessArgument()} --key-id \"KEY123\" --issuer \"ISSUER456\"", args);
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_IncompleteAppleId_ThrowsInvalidOperationException()
    {
        var build = new TestMacOSBuild();
        build.SetMacAppleId("dev@example.com");
        build.SetMacPassword("app-password");
        // missing team id
        var ex = Assert.Throws<InvalidOperationException>(() => build.InvokeGetNotaryToolAuthenticationArguments());
        Assert.Contains("requires MacAppleId, MacPassword, and MacTeamId to all be specified", ex.Message);
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_IncompleteApiKey_ThrowsInvalidOperationException()
    {
        var build = new TestMacOSBuild();
        build.SetMacApiKey((AbsolutePath)"/keys/AuthKey.p8", "KEY123", null);
        var ex = Assert.Throws<InvalidOperationException>(() => build.InvokeGetNotaryToolAuthenticationArguments());
        Assert.Contains("requires MacApiKeyPath, MacApiKeyId, and MacApiIssuerId to all be specified", ex.Message);
    }

    [Fact]
    public void GetNotaryToolAuthenticationArguments_NoCredentials_ThrowsInvalidOperationException()
    {
        var build = new TestMacOSBuild();
        var ex = Assert.Throws<InvalidOperationException>(() => build.InvokeGetNotaryToolAuthenticationArguments());
        Assert.Contains("notarization credentials were not configured", ex.Message);
    }

    [Fact]
    public void SignMacOSApp_WhenUnconfigured_PreservesAdHocCodesign()
    {
        using var temp = new TemporaryDirectory(prefix: "test-sign");
        var appPath = (AbsolutePath)Path.Combine(temp.DirectoryPath, "TestApp.app");
        Directory.CreateDirectory(appPath);

        var build = new TestMacOSBuild();
        build.InvokeSignMacOSApp(appPath);

        Assert.Single(build.CodeSignArguments);
        Assert.Equal($"--force --deep --sign - {appPath.ToString().QuoteProcessArgument()}", build.CodeSignArguments[0]);
        Assert.Empty(build.NotaryToolSubmitCalls);
        Assert.Empty(build.StaplerCalls);
    }

    [Fact]
    public void SignMacOSApp_WithSigningIdentity_ExecutesHardenedRuntimeAndTimestamp()
    {
        using var temp = new TemporaryDirectory(prefix: "test-sign");
        var appPath = (AbsolutePath)Path.Combine(temp.DirectoryPath, "TestApp.app");
        Directory.CreateDirectory(appPath);

        var build = new TestMacOSBuild();
        build.SetMacSigningIdentity("Developer ID Application: Contoso Inc (TEAM123)");
        build.InvokeSignMacOSApp(appPath);

        Assert.Single(build.CodeSignArguments);
        var arg = build.CodeSignArguments[0];
        Assert.Contains("--force --deep --timestamp --options runtime", arg);
        Assert.Contains("--sign \"Developer ID Application: Contoso Inc (TEAM123)\"", arg);
        Assert.Contains(appPath.ToString().QuoteProcessArgument(), arg);
        Assert.Empty(build.NotaryToolSubmitCalls);
        Assert.Empty(build.StaplerCalls);
    }

    [Fact]
    public void SignMacOSApp_WithEntitlementsFile_IncludesEntitlementsArgument()
    {
        using var temp = new TemporaryDirectory(prefix: "test-sign");
        var appPath = (AbsolutePath)Path.Combine(temp.DirectoryPath, "TestApp.app");
        var contentsPath = (AbsolutePath)Path.Combine(appPath, "Contents");
        Directory.CreateDirectory(contentsPath);
        var entitlementsFile = contentsPath / "TestApp.entitlements";
        File.WriteAllText(entitlementsFile, "<plist></plist>");

        var build = new TestMacOSBuild();
        build.SetMacSigningIdentity("Developer ID Application: Contoso");
        build.InvokeSignMacOSApp(appPath);

        Assert.Single(build.CodeSignArguments);
        Assert.Contains($"--entitlements {entitlementsFile.ToString().QuoteProcessArgument()}", build.CodeSignArguments[0]);
    }

    [Fact]
    public void SignMacOSApp_WithNotarization_ExecutesDeveloperIdSignAndSubmitsForNotarization()
    {
        using var temp = new TemporaryDirectory(prefix: "test-sign");
        var appPath = (AbsolutePath)Path.Combine(temp.DirectoryPath, "TestApp.app");
        Directory.CreateDirectory(appPath);

        var build = new TestMacOSBuild
        {
            TestPublishStagingDirectory = (AbsolutePath)temp.DirectoryPath
        };
        build.SetMacKeychainProfile("profile1");
        build.InvokeSignMacOSApp(appPath);

        Assert.Single(build.CodeSignArguments);
        Assert.Contains("--sign \"Developer ID Application\"", build.CodeSignArguments[0]);
        Assert.Single(build.DittoZipCalls);
        Assert.Equal(appPath, build.DittoZipCalls[0].Source);
        Assert.Single(build.NotaryToolSubmitCalls);
        Assert.Equal("--keychain-profile \"profile1\"", build.NotaryToolSubmitCalls[0].AuthArgs);
        Assert.Single(build.StaplerCalls);
        Assert.Equal(appPath, build.StaplerCalls[0]);
    }

    [Fact]
    public void NotarizeMacOSPackage_SubmitsPackageAndStaplesTarget()
    {
        using var temp = new TemporaryDirectory(prefix: "test-pkg");
        var pkgPath = (AbsolutePath)Path.Combine(temp.DirectoryPath, "TestApp.pkg");
        File.WriteAllText(pkgPath, "dummy-pkg");

        var build = new TestMacOSBuild();
        build.SetMacKeychainProfile("profile1");
        build.InvokeNotarizeMacOSPackage(pkgPath);

        Assert.Single(build.NotaryToolSubmitCalls);
        Assert.Equal(pkgPath, build.NotaryToolSubmitCalls[0].SubmissionPath);
        Assert.Equal("--keychain-profile \"profile1\"", build.NotaryToolSubmitCalls[0].AuthArgs);
        Assert.Single(build.StaplerCalls);
        Assert.Equal(pkgPath, build.StaplerCalls[0]);
    }

    private sealed class TestMacOSBuild : StageKitBuild
    {
        public List<string> CodeSignArguments { get; } = [];
        public List<(AbsolutePath Source, AbsolutePath Destination)> DittoZipCalls { get; } = [];
        public List<(AbsolutePath SubmissionPath, string AuthArgs)> NotaryToolSubmitCalls { get; } = [];
        public List<AbsolutePath> StaplerCalls { get; } = [];

        public AbsolutePath? TestPublishStagingDirectory { get; set; }
        protected override AbsolutePath PublishStagingDirectory => TestPublishStagingDirectory ?? base.PublishStagingDirectory;

        public MacAppBundleOptions TestMacAppBundleOptions { get; } = new()
        {
            ProductName = "TestApp",
            BundleIdentifier = "com.test.app",
            Version = "1.0.0"
        };
        protected override MacAppBundleOptions CreateMacAppBundleOptions() => TestMacAppBundleOptions;

        public override string SoftwareName => "TestApp";

        public void SetMacSigningIdentity(string? identity) => MacSigningIdentity = identity;
        public void SetMacNotarize(bool notarize) => MacNotarize = notarize;
        public void SetMacKeychainProfile(string? profile) => MacKeychainProfile = profile;
        public void SetMacAppleId(string? appleId) => MacAppleId = appleId;
        public void SetMacPassword(string? password) => MacPassword = password;
        public void SetMacTeamId(string? teamId) => MacTeamId = teamId;
        public void SetMacApiKey(AbsolutePath? keyPath, string? keyId, string? issuerId)
        {
            MacApiKeyPath = keyPath;
            MacApiKeyId = keyId;
            MacApiIssuerId = issuerId;
        }

        public bool InvokeHasMacNotarizationCredentials() => HasMacNotarizationCredentials;
        public bool InvokeShouldNotarizeMacOS() => ShouldNotarizeMacOS;
        public string InvokeGetNotaryToolAuthenticationArguments() => GetNotaryToolAuthenticationArguments();
        public void InvokeSignMacOSApp(AbsolutePath appPath) => SignMacOSApp(appPath);
        public void InvokeNotarizeMacOSApp(AbsolutePath appPath) => NotarizeMacOSApp(appPath);
        public void InvokeNotarizeMacOSPackage(AbsolutePath packagePath) => NotarizeMacOSPackage(packagePath);

        protected override void ExecuteCodeSign(string arguments)
        {
            CodeSignArguments.Add(arguments);
        }

        protected override void ExecuteDittoZip(AbsolutePath source, AbsolutePath destinationZip)
        {
            DittoZipCalls.Add((source, destinationZip));
            File.WriteAllText(destinationZip, "dummy-zip");
        }

        protected override void ExecuteNotaryToolSubmit(AbsolutePath submissionPath, string authenticationArguments)
        {
            NotaryToolSubmitCalls.Add((submissionPath, authenticationArguments));
        }

        protected override void ExecuteStapler(AbsolutePath targetPath)
        {
            StaplerCalls.Add(targetPath);
        }
    }
}

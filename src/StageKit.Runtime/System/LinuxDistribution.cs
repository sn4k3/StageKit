namespace StageKit.Runtime.System;

/// <summary>
/// Represents a Linux distribution with its associated properties.
/// </summary>
/// <param name="Id">The ID of the Linux distribution.</param>
/// <param name="Name">The name of the Linux distribution.</param>
/// <param name="PrettyName">The pretty name of the Linux distribution.</param>
/// <param name="VersionId">The version ID of the Linux distribution.</param>
/// <param name="VersionCodename">The version codename of the Linux distribution.</param>
/// <param name="Version">The version of the Linux distribution.</param>
/// <param name="IdLike">The IDs of distributions that are like this distribution.</param>
/// <param name="HomeUrl">The home URL of the Linux distribution.</param>
/// <param name="SupportUrl">The support URL of the Linux distribution.</param>
/// <param name="BugReportUrl">The bug report URL of the Linux distribution.</param>
/// <param name="PrivacyPolicyUrl">The privacy policy URL of the Linux distribution.</param>
public sealed record LinuxDistribution(
    string? Id,
    string? Name,
    string? PrettyName,
    string? VersionId,
    string? VersionCodename,
    string? Version,
    string[] IdLike,
    string? HomeUrl,
    string? SupportUrl,
    string? BugReportUrl,
    string? PrivacyPolicyUrl
);
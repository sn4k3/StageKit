namespace StageKit.Runtime.System;

/// <summary>
/// Represents the various Linux package managers.
/// </summary>
public enum LinuxPackageManager
{
    /// <summary>
    /// An unknown or unsupported Linux package manager.
    /// </summary>
    Unknown,

    /// <summary>
    /// The APT package manager (Debian, Ubuntu, and derivatives).
    /// </summary>
    Apt,

    /// <summary>
    /// The DNF5 package manager (Fedora, RHEL, CentOS, and derivatives).
    /// </summary>
    Dnf5,

    /// <summary>
    /// The DNF package manager (Fedora, RHEL, CentOS, and derivatives).
    /// </summary>
    Dnf,

    /// <summary>
    /// The YUM package manager (older Fedora, RHEL, CentOS, and derivatives).
    /// </summary>
    Yum,

    /// <summary>
    /// The Zypper package manager (openSUSE and derivatives).
    /// </summary>
    Zypper,

    /// <summary>
    /// The Pacman package manager (Arch Linux and derivatives).
    /// </summary>
    Pacman,

    /// <summary>
    /// The APK package manager (Alpine Linux).
    /// </summary>
    Apk,

    /// <summary>
    /// The XBPS package manager (Void Linux and derivatives).
    /// </summary>
    Xbps,

    /// <summary>
    /// The Emerge package manager (Gentoo and derivatives).
    /// </summary>
    Emerge
}
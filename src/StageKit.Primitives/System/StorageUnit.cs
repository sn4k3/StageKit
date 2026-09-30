#region

using System.ComponentModel;

#endregion

namespace StageKit.Primitives.System;

/// <summary>
/// Represents units of digital information storage.
/// </summary>
public enum StorageUnit
{
    /// <summary>
    /// Represents a byte, the basic unit of digital information storage.
    /// </summary>
    [Description("Byte")] B,

    /// <summary>
    /// Represents a kilobyte, which is equal to 1024 bytes.
    /// </summary>
    [Description("Kilobyte")] KB,

    /// <summary>
    /// Represents a megabyte, which is equal to 1024 kilobytes.
    /// </summary>
    [Description("Megabyte")] MB,

    /// <summary>
    /// Represents a gigabyte, which is equal to 1024 megabytes.
    /// </summary>
    [Description("Gigabyte")] GB,

    /// <summary>
    /// Represents a terabyte, which is equal to 1024 gigabytes.
    /// </summary>
    [Description("Terabyte")] TB,

    /// <summary>
    /// Represents a petabyte, which is equal to 1024 terabytes.
    /// </summary>
    [Description("Petabyte")] PB,

    /// <summary>
    /// Represents an exabyte, which is equal to 1024 petabytes.
    /// </summary>
    [Description("Exabyte")]  EB
}

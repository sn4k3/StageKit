using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StageKit.Runtime;

/// <summary>
/// Describes the runtime and packaging metadata for a published build.
/// </summary>
public record BuildRuntime
{
    private static readonly Lazy<BuildRuntime?> InstanceLazy = new(LoadInstance);

    /// <summary>
    /// The default name of the runtime manifest emitted by StageKit.Fallout.
    /// </summary>
    public const string DefaultManifestFileName = "build-runtime.json";

    /// <summary>
    /// Gets the lazily loaded build runtime manifest for the current application, if available and valid.
    /// </summary>
    /// <remarks>
    /// The manifest is loaded at most once. The application base directory is checked first, followed by the entry
    /// assembly and process directories. Missing, inaccessible, and invalid manifests produce <see langword="null"/>.
    /// </remarks>
    public static BuildRuntime? Instance => InstanceLazy.Value;

    /// <summary>
    /// Gets a value indicating whether the singleton manifest lookup has run.
    /// </summary>
    public static bool IsInstanceCreated => InstanceLazy.IsValueCreated;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildRuntime"/> class.
    /// </summary>
    public BuildRuntime()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildRuntime"/> class with build values.
    /// </summary>
    /// <param name="runtime">The runtime identifier for the build.</param>
    /// <param name="buildVersion">The application version used for the build.</param>
    /// <param name="isBundle">Whether the build is a bundle.</param>
    /// <param name="packagingType">The packaging type represented by the build.</param>
    [SetsRequiredMembers]
    public BuildRuntime(string runtime, string buildVersion, bool isBundle = false,
        ApplicationPackagingType packagingType = default)
    {
        Runtime = runtime;
        BuildVersion = buildVersion;
        IsBundle = isBundle;
        PackagingType = packagingType;
    }

    /// <summary>
    /// Gets the version of the runtime manifest schema.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets or sets the runtime identifier for the build.
    /// </summary>
    public required string Runtime { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the build is a bundle.
    /// </summary>
    public bool IsBundle { get; init; }

    /// <summary>
    /// Gets or sets the packaging type represented by the build.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ApplicationPackagingType>))]
    public ApplicationPackagingType PackagingType { get; init; }

    /// <summary>
    /// Gets or sets the UTC date and time when the build metadata was created.
    /// </summary>
    public DateTime BuildDateTimeUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the operating system description of the build environment.
    /// </summary>
    public string BuildOSDescription { get; init; } = RuntimeInformation.OSDescription;

    /// <summary>
    /// Gets or sets the application version used for the build.
    /// </summary>
    public required string BuildVersion { get; init; }

    /// <summary>
    /// Tries to load and deserialize a build runtime manifest from disk.
    /// </summary>
    /// <param name="filePath">The manifest file path.</param>
    /// <param name="runtime">The deserialized manifest when successful; otherwise, <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when a non-null manifest was deserialized; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Deserialization uses source-generated metadata and is compatible with trimming and Native AOT. Expected file,
    /// access, and JSON errors are treated as an unavailable manifest.
    /// </remarks>
    public static bool TryLoad(string filePath, [NotNullWhen(true)] out BuildRuntime? runtime)
    {
        runtime = null;

        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            using var stream = File.OpenRead(filePath);
            runtime = JsonSerializer.Deserialize(
                stream,
                BuildRuntimeJsonContext.Default.BuildRuntime);
            return runtime is not null;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          UnauthorizedAccessException or
                                          NotSupportedException or
                                          JsonException)
        {
            return false;
        }
    }

    internal static BuildRuntime? LoadFromApplicationDirectories(
        string? appContextBaseDirectory,
        string? assemblyLocation,
        string? processPath)
    {
        var directories = new[]
        {
            appContextBaseDirectory,
            GetDirectoryName(assemblyLocation),
            GetDirectoryName(processPath)
        };

        foreach (var directory in directories.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(
                     OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory!, DefaultManifestFileName);
            if (TryLoad(manifestPath, out var runtime))
                return runtime;
        }

        return null;

        static string? GetDirectoryName(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
        }
    }

    private static BuildRuntime? LoadInstance()
    {
        return LoadFromApplicationDirectories(
            AppContext.BaseDirectory,
            EntryApplication.AssemblyLocation,
            Environment.ProcessPath);
    }
}

[JsonSerializable(typeof(BuildRuntime))]
internal sealed partial class BuildRuntimeJsonContext : JsonSerializerContext;

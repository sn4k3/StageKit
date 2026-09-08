using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    private const string DisplayAdapterClassRegistryPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private static readonly TimeSpan HardwareQueryTimeout = TimeSpan.FromSeconds(5);
    private static readonly Lazy<string?> SystemManufacturerLazy = new(GetSystemManufacturer);
    private static readonly Lazy<string?> SystemModelLazy = new(GetSystemModel);
    private static readonly Lazy<string?> ProcessorNameLazy = new(GetProcessorName);
    private static readonly Lazy<IReadOnlyList<string>> GraphicsCardNamesLazy = new(GetGraphicsCardNames);

    /// <summary>
    /// Gets the product name of the current operating system.
    /// </summary>
    /// <remarks>
    /// The value is detected once per process and is suitable for display. Unsupported platforms return
    /// <c>Unknown</c>.
    /// </remarks>
    [field: AllowNull]
    [field: MaybeNull]
    public static string OperatingSystemName => field ??= GetOperatingSystemName();

    /// <summary>
    /// Gets the current operating-system name followed by its architecture.
    /// </summary>
    public static string OperatingSystemNameWithArch => $"{OperatingSystemName} {RuntimeInformation.OSArchitecture}";

    /// <summary>
    /// Gets the manufacturer of the current host, or <see langword="null"/> when it cannot be determined.
    /// </summary>
    /// <remarks>A successful result is cached. A failed query is retried on the next access.</remarks>
    public static string? SystemManufacturer => SystemManufacturerLazy.Value ?? GetSystemManufacturer();

    /// <summary>
    /// Gets the model of the current host, or <see langword="null"/> when it cannot be determined.
    /// </summary>
    /// <remarks>A successful result is cached. A failed query is retried on the next access.</remarks>
    public static string? SystemModel => SystemModelLazy.Value ?? GetSystemModel();

    /// <summary>
    /// Gets the elapsed time since the operating system started.
    /// </summary>
    /// <returns>The operating-system uptime reported by the runtime.</returns>
    public static TimeSpan SystemUptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>
    /// Gets the display name of the current host's processor, or <see langword="null"/> when it cannot be determined.
    /// </summary>
    /// <remarks>
    /// A successful result is cached because processor hardware does not change while the process is running. A failed
    /// query is retried on the next access.
    /// </remarks>
    public static string? ProcessorName => ProcessorNameLazy.Value ?? GetProcessorName();

    /// <summary>
    /// Gets the display names of the graphics adapters detected on the current host.
    /// </summary>
    /// <remarks>
    /// A successful result is cached. An empty result is retried on the next access. The returned collection is
    /// read-only. Windows excludes indirect USB and software display drivers that do not represent graphics hardware.
    /// </remarks>
    public static IReadOnlyList<string> GraphicsCardNames
    {
        get
        {
            var names = GraphicsCardNamesLazy.Value;
            return names.Count > 0 ? names : GetGraphicsCardNames();
        }
    }

    /// <summary>
    /// Gets the first graphics-adapter display name reported by the operating system, or <see langword="null"/> when
    /// no adapter can be determined.
    /// </summary>
    public static string? GraphicsCardName => GraphicsCardNames.FirstOrDefault();

    /// <summary>
    /// Gets the first graphics-adapter display name reported by the operating system, or <see langword="null"/> when
    /// no adapter can be determined.
    /// </summary>
    /// <remarks>Use <see cref="GraphicsCardName"/> for new code.</remarks>
    public static string? GraphicCardName => GraphicsCardName;

    private static string GetOperatingSystemName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacCatalyst()) return "Mac Catalyst";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsAndroid()) return "Android";
        if (OperatingSystem.IsIOS()) return "iOS";
        if (OperatingSystem.IsTvOS()) return "tvOS";
        if (OperatingSystem.IsWatchOS()) return "watchOS";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsFreeBSD()) return "FreeBSD";
        if (OperatingSystem.IsBrowser()) return "Browser";
        if (OperatingSystem.IsWasi()) return "WASI";
        return "Unknown";
    }

    private static string? GetSystemManufacturer()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetWindowsBiosValue("SystemManufacturer")
                       ?? GetWindowsBiosValue("BaseBoardManufacturer");
            }

            if (OperatingSystem.IsLinux())
                return GetSystemInformationFileValue("/sys/devices/virtual/dmi/id/sys_vendor");

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
                return "Apple Inc.";

            if (OperatingSystem.IsFreeBSD())
                return NormalizeSystemInformationValue(
                    GetBoundedProcessOutput("kenv", ["-q", "smbios.system.maker"]));
        }
        catch (Exception e) when (IsExpectedHardwareQueryException(e))
        {
            Debug.WriteLine(e);
        }

        return null;
    }

    private static string? GetSystemModel()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetWindowsBiosValue("SystemProductName")
                       ?? GetWindowsBiosValue("BaseBoardProduct");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetSystemInformationFileValue("/sys/devices/virtual/dmi/id/product_name")
                       ?? GetSystemInformationFileValue("/sys/firmware/devicetree/base/model");
            }

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
                return NormalizeSystemInformationValue(TryGetMacSysctlString("hw.model"));

            if (OperatingSystem.IsFreeBSD())
                return NormalizeSystemInformationValue(
                    GetBoundedProcessOutput("kenv", ["-q", "smbios.system.product"]));
        }
        catch (Exception e) when (IsExpectedHardwareQueryException(e))
        {
            Debug.WriteLine(e);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? GetWindowsBiosValue(string valueName)
    {
        using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        return NormalizeSystemInformationValue(bios?.GetValue(valueName) as string);
    }

    private static string? GetSystemInformationFileValue(string path)
    {
        return File.Exists(path)
            ? NormalizeSystemInformationValue(File.ReadAllText(path))
            : null;
    }

    internal static string? NormalizeSystemInformationValue(string? value)
    {
        var normalized = value?.Trim().TrimEnd('\0').Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Equals("Default string", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Not Applicable", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("System manufacturer", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("System Product Name", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    internal static string? ParseLinuxProcessorName(ReadOnlySpan<char> text)
    {
        string? bestMatch = null;
        var bestPriority = int.MaxValue;

        while (!text.IsEmpty)
        {
            var newline = text.IndexOf('\n');
            var line = newline < 0 ? text : text[..newline];
            text = newline < 0 ? [] : text[(newline + 1)..];

            ConsiderProcessorInformationLine(line, ref bestMatch, ref bestPriority);
        }

        return bestMatch;
    }

    internal static IReadOnlyList<string> ParseLspciGraphicsCardNames(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var names = new List<string>();
        foreach (var line in output.AsSpan().EnumerateLines())
        {
            var fields = ParseQuotedFields(line);
            if (fields.Count < 4 || !IsGraphicsControllerClass(fields[1]))
                continue;

            var vendor = RemoveTrailingPciIdentifier(fields[2]);
            var device = RemoveTrailingPciIdentifier(fields[3]);
            AddUniqueName(names, string.Concat(vendor, " ", device));
        }

        return new ReadOnlyCollection<string>(names);
    }

    internal static IReadOnlyList<string> ParseMacGraphicsCardNames(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var names = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(output);
            if (!document.RootElement.TryGetProperty("SPDisplaysDataType", out var adapters)
                || adapters.ValueKind != JsonValueKind.Array)
            {
                return new ReadOnlyCollection<string>(names);
            }

            foreach (var adapter in adapters.EnumerateArray())
            {
                if (TryGetNonEmptyJsonString(adapter, "sppci_model", out var name)
                    || TryGetNonEmptyJsonString(adapter, "_name", out name))
                {
                    AddUniqueName(names, name);
                }
            }
        }
        catch (JsonException e)
        {
            Debug.WriteLine(e);
        }

        return new ReadOnlyCollection<string>(names);
    }

    private static string? GetProcessorName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return GetWindowsProcessorName();

            if (OperatingSystem.IsLinux())
                return File.Exists("/proc/cpuinfo")
                    ? GetLinuxProcessorName()
                    : null;

            if (OperatingSystem.IsMacOS())
                return TryGetMacSysctlString("machdep.cpu.brand_string")
                       ?? TryGetMacSysctlString("hw.model");

            if (OperatingSystem.IsFreeBSD())
                return GetBoundedProcessOutput("sysctl", ["-n", "hw.model"]);
        }
        catch (Exception e) when (IsExpectedHardwareQueryException(e))
        {
            Debug.WriteLine(e);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? GetWindowsProcessorName()
    {
        using var processors = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor");
        if (processors is null)
            return null;

        foreach (var subKeyName in processors.GetSubKeyNames())
        {
            if (!int.TryParse(subKeyName, out _))
                continue;

            try
            {
                using var processor = processors.OpenSubKey(subKeyName);
                var name = processor?.GetValue("ProcessorNameString") as string;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }
            catch (Exception e) when (IsExpectedHardwareQueryException(e))
            {
                Debug.WriteLine(e);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetGraphicsCardNames()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return GetWindowsGraphicsCardNames();

            if (OperatingSystem.IsLinux())
                return GetLinuxGraphicsCardNames();

            if (OperatingSystem.IsMacOS())
                return GetMacGraphicsCardNames();
        }
        catch (Exception e) when (IsExpectedHardwareQueryException(e))
        {
            Debug.WriteLine(e);
        }

        return [];
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> GetWindowsGraphicsCardNames()
    {
        var names = new List<string>();
        using var adapters = Registry.LocalMachine.OpenSubKey(DisplayAdapterClassRegistryPath);
        if (adapters is null)
            return new ReadOnlyCollection<string>(names);

        foreach (var subKeyName in adapters.GetSubKeyNames())
        {
            // The class key also contains protected metadata subkeys which are not adapter instances.
            if (!int.TryParse(subKeyName, out _))
                continue;

            try
            {
                using var adapter = adapters.OpenSubKey(subKeyName);
                var matchingDeviceId = adapter?.GetValue("MatchingDeviceId") as string;
                if (!IsWindowsHardwareGraphicsAdapter(matchingDeviceId))
                    continue;

                var name = adapter?.GetValue("DriverDesc") as string;
                AddUniqueName(names, name);
            }
            catch (Exception e) when (IsExpectedHardwareQueryException(e))
            {
                Debug.WriteLine(e);
            }
        }

        return new ReadOnlyCollection<string>(names);
    }

    internal static bool IsWindowsHardwareGraphicsAdapter(string? matchingDeviceId)
    {
        if (string.IsNullOrWhiteSpace(matchingDeviceId))
            return false;

        return matchingDeviceId.StartsWith(@"pci\", StringComparison.OrdinalIgnoreCase) ||
               matchingDeviceId.StartsWith(@"acpi\", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetLinuxGraphicsCardNames()
    {
        var output = GetBoundedProcessOutput("lspci", ["-Dmm", "-nn"]);
        if (!string.IsNullOrWhiteSpace(output))
        {
            var names = ParseLspciGraphicsCardNames(output);
            if (names.Count > 0)
                return names;
        }

        return GetLinuxSysfsGraphicsCardNames();
    }

    private static IReadOnlyList<string> GetLinuxSysfsGraphicsCardNames()
    {
        var names = new List<string>();
        const string drmPath = "/sys/class/drm";
        if (!Directory.Exists(drmPath))
            return new ReadOnlyCollection<string>(names);

        foreach (var directory in Directory.EnumerateDirectories(drmPath, "card*"))
        {
            var suffix = Path.GetFileName(directory).AsSpan(4);
            if (suffix.IsEmpty || !int.TryParse(suffix, out _))
                continue;

            var devicePath = Path.Combine(directory, "device");
            var productNamePath = Path.Combine(devicePath, "product_name");
            if (File.Exists(productNamePath))
            {
                AddUniqueName(names, File.ReadAllText(productNamePath));
                continue;
            }

            var vendorPath = Path.Combine(devicePath, "vendor");
            var deviceIdPath = Path.Combine(devicePath, "device");
            if (File.Exists(vendorPath) && File.Exists(deviceIdPath))
            {
                var vendor = File.ReadAllText(vendorPath).Trim();
                var device = File.ReadAllText(deviceIdPath).Trim();
                AddUniqueName(names, $"PCI {vendor}:{device}");
            }
        }

        return new ReadOnlyCollection<string>(names);
    }

    private static IReadOnlyList<string> GetMacGraphicsCardNames()
    {
        var output = GetBoundedProcessOutput(
            "system_profiler",
            ["SPDisplaysDataType", "-json", "-detailLevel", "mini"]);
        return string.IsNullOrWhiteSpace(output)
            ? []
            : ParseMacGraphicsCardNames(output);
    }

    private static string? GetBoundedProcessOutput(string executable, IEnumerable<string> arguments)
    {
        using var cancellationSource = new CancellationTokenSource(HardwareQueryTimeout);
        try
        {
            var result = ProcessHelper
                .GetProcessOutputAsync(executable, arguments, cancellationToken: cancellationSource.Token)
                .GetAwaiter()
                .GetResult();
            return result.Succeeded ? result.StandardOutput.Trim() : null;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string? GetLinuxProcessorName()
    {
        string? bestMatch = null;
        var bestPriority = int.MaxValue;
        foreach (var line in File.ReadLines("/proc/cpuinfo"))
            ConsiderProcessorInformationLine(line, ref bestMatch, ref bestPriority);
        return bestMatch;
    }

    private static void ConsiderProcessorInformationLine(
        ReadOnlySpan<char> line,
        ref string? bestMatch,
        ref int bestPriority)
    {
        var colon = line.IndexOf(':');
        if (colon < 0)
            return;

        var key = line[..colon].Trim();
        var value = line[(colon + 1)..].Trim();
        if (value.IsEmpty)
            return;

        var priority = GetProcessorKeyPriority(key);
        if (priority >= bestPriority)
            return;

        // The common "processor: 0" lines identify logical processors rather than a processor model.
        if (key.Equals("processor", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value, out _))
        {
            return;
        }

        bestMatch = value.ToString();
        bestPriority = priority;
    }

    private static string? TryGetMacSysctlString(string name)
    {
        nuint length = 0;
        if (SysctlStringByName(name, 0, ref length, 0, 0) != 0
            || length <= 1
            || length > int.MaxValue)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((nint)length);
        try
        {
            if (SysctlStringByName(name, buffer, ref length, 0, 0) != 0 || length <= 1)
                return null;

            return Marshal.PtrToStringUTF8(buffer, checked((int)length - 1))?.Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int GetProcessorKeyPriority(ReadOnlySpan<char> key)
    {
        if (key.Equals("model name", StringComparison.OrdinalIgnoreCase)) return 0;
        if (key.Equals("cpu model", StringComparison.OrdinalIgnoreCase)) return 1;
        if (key.Equals("processor", StringComparison.OrdinalIgnoreCase)) return 2;
        if (key.Equals("hardware", StringComparison.OrdinalIgnoreCase)) return 3;
        return int.MaxValue;
    }

    private static List<string> ParseQuotedFields(ReadOnlySpan<char> line)
    {
        var fields = new List<string>(4);
        var index = 0;
        while (index < line.Length && fields.Count < 4)
        {
            while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
            if (index >= line.Length) break;

            if (line[index] == '"')
            {
                var start = ++index;
                while (index < line.Length && line[index] != '"') index++;
                fields.Add(line[start..index].ToString());
                if (index < line.Length) index++;
                continue;
            }

            var unquotedStart = index;
            while (index < line.Length && !char.IsWhiteSpace(line[index])) index++;
            fields.Add(line[unquotedStart..index].ToString());
        }

        return fields;
    }

    /// <summary>
    /// Determines whether the given PCI class identifier indicates a graphics controller.
    /// </summary>
    /// <param name="value">The PCI class identifier to check.</param>
    /// <returns><c>true</c> if the PCI class identifier indicates a graphics controller; otherwise, <c>false</c>.</returns>
    private static bool IsGraphicsControllerClass(string value)
    {
        return value.Contains("[0300]", StringComparison.OrdinalIgnoreCase)
               || value.Contains("[0302]", StringComparison.OrdinalIgnoreCase)
               || value.Contains("[0380]", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes a trailing PCI identifier from a string, if present. A trailing PCI identifier is defined as a substring that starts with an opening square bracket '[' and ends with a closing square bracket ']', and is located at the end of the string.
    /// </summary>
    /// <param name="value">The string from which to remove the trailing PCI identifier.</param>
    /// <returns>The string with the trailing PCI identifier removed, if present; otherwise, the original string.</returns>
    private static string RemoveTrailingPciIdentifier(string value)
    {
        var trimmed = value.AsSpan().Trim();
        var bracket = trimmed.LastIndexOf('[');
        if (bracket > 0 && trimmed.EndsWith("]", StringComparison.Ordinal))
            trimmed = trimmed[..bracket].TrimEnd();
        return trimmed.ToString();
    }

    /// <summary>
    /// Adds a unique name to the list of names, ignoring case. If the name is null, empty, or already exists in the list (case-insensitive), it is not added.
    /// </summary>
    /// <param name="names">The list of names to which the unique name will be added.</param>
    /// <param name="value">The name to add.</param>
    private static void AddUniqueName(List<string> names, string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)
            || names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        names.Add(name);
    }

    private static bool TryGetNonEmptyJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool IsExpectedHardwareQueryException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or SecurityException
            or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException;
    }

    [LibraryImport(MacSystemLibrary, EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SysctlStringByName(
        string name,
        nint oldValue,
        ref nuint oldLength,
        nint newValue,
        nuint newLength);
}
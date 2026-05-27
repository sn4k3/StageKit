using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace StageKit.Runtime;

/// <summary>
/// Provides diagnostic information about the current runtime, process, and entry application.
/// </summary>
public static class RuntimeDiagnostics
{
    /// <summary>
    /// Returns a dictionary containing runtime, process, and entry-application diagnostic values.
    /// </summary>
    /// <returns>A dictionary containing diagnostic key-value pairs for the current process.</returns>
    public static Dictionary<string, string?> GetInfoDict()
    {
        var info = new Dictionary<string, string?>(64)
        {
            ["Runtime.FrameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["Runtime.RuntimeIdentifier"] = RuntimeInformation.RuntimeIdentifier,
            ["Runtime.GenericRuntimeIdentifier"] = EntryApplication.GenericRuntimeIdentifier,
            ["Runtime.OSDescription"] = RuntimeInformation.OSDescription,
            ["Runtime.OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["Runtime.ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["Process.Id"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["Process.Name"] = EntryApplication.ProcessName,
            ["Process.Path"] = Environment.ProcessPath,
            ["Process.BaseDirectory"] = AppContext.BaseDirectory,
            ["Process.CurrentDirectory"] = Environment.CurrentDirectory
        };

        foreach (var kvp in EntryApplication.GetApplicationInfoDict())
        {
            info[$"EntryApplication.{kvp.Key}"] = kvp.Value;
        }

        return info;
    }

    /// <summary>
    /// Returns a formatted diagnostic report for the current runtime, process, and entry application.
    /// </summary>
    /// <param name="includeLoadedAssemblies">True to append the currently loaded assembly list; otherwise, false.</param>
    /// <returns>A formatted diagnostic report.</returns>
    public static string GetReport(bool includeLoadedAssemblies = false)
    {
        var info = GetInfoDict();
        var sb = new StringBuilder(info.Count * 64);

        foreach (var kvp in info)
        {
            sb.AppendLine($"{kvp.Key}: {kvp.Value?.ReplaceLineEndings("\\n")}");
        }

        if (includeLoadedAssemblies)
        {
            sb.AppendLine();
            sb.AppendLine("Loaded Assemblies:");
            sb.AppendLine(EntryApplication.FormattedLoadedAssemblies);
        }

        return sb.ToString().TrimEnd();
    }
}

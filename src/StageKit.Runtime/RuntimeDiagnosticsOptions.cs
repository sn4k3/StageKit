namespace StageKit.Runtime;

/// <summary>
/// Configures the information included in a runtime diagnostics report.
/// </summary>
public sealed class RuntimeDiagnosticsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether current process and managed-runtime measurements are included.
    /// </summary>
    public bool IncludeProcessSnapshot { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the currently loaded assembly list is appended to the report.
    /// </summary>
    public bool IncludeLoadedAssemblies { get; set; }
}

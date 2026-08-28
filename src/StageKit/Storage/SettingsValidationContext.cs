namespace StageKit;

/// <summary>
/// Provides information and change tracking to a settings validation hook.
/// </summary>
public sealed class SettingsValidationContext
{
    private readonly List<string> _messages = [];

    /// <summary>
    /// Gets a value indicating whether validation is running for settings loaded from an existing file.
    /// </summary>
    public bool FromFile { get; }

    /// <summary>
    /// Gets a value indicating whether validation repaired or otherwise changed the settings object.
    /// </summary>
    public bool HasChanges { get; private set; }

    /// <summary>
    /// Gets validation and repair messages recorded by <see cref="MarkChanged"/>.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsValidationContext"/> class.
    /// </summary>
    /// <param name="fromFile">Whether validation is running for settings loaded from an existing file.</param>
    public SettingsValidationContext(bool fromFile)
    {
        FromFile = fromFile;
    }

    /// <summary>
    /// Marks the settings object as changed by validation.
    /// </summary>
    /// <param name="reason">The reason validation changed the settings.</param>
    public void MarkChanged(string reason)
    {
        HasChanges = true;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _messages.Add(reason);
        }
    }
}

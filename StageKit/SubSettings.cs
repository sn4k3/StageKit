namespace StageKit;

/// <summary>
/// Base type for nested or root settings objects that need change notification support.
/// </summary>
public class SubSettings : ObservableObjectKit
{
    /// <summary>
    /// Runs after a settings object is loaded or created.
    /// </summary>
    /// <param name="fromFile">
    /// <see langword="true"/> when the settings object was deserialized from disk; otherwise,
    /// <see langword="false"/>.
    /// </param>
    public virtual void OnLoaded(bool fromFile)
    {

    }
}

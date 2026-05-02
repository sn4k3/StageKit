using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StageKit;

/// <summary>
/// Base type for nested or root settings objects that need change notification support.
/// </summary>
public class SubSettings : ObservableObject
{
    /// <summary>
    /// Indicates whether the settings object has been loaded or created. This is <see langword="false"/> until
    /// <see cref="OnLoadedCore"/> is called.
    /// </summary>
    [JsonIgnore]
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Runs after a settings object is loaded or created.
    /// </summary>
    /// <param name="fromFile">
    /// <see langword="true"/> when the settings object was deserialized from disk; otherwise,
    /// <see langword="false"/>.
    /// </param>
    private protected void OnLoadedCore(bool fromFile)
    {
        OnLoaded(fromFile);
        IsLoaded = true;
    }

    /// <summary>
    /// Runs after a settings object is loaded or created.
    /// </summary>
    /// <param name="fromFile">
    /// <see langword="true"/> when the settings object was deserialized from disk; otherwise,
    /// <see langword="false"/>.
    /// </param>
    protected virtual void OnLoaded(bool fromFile)
    {
    }
}

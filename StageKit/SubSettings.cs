using System.ComponentModel;
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
    /// Indicates whether the settings object has unsaved changes. This is set to <see langword="true"/> when any property changes, and should be set to <see langword="false"/> when the settings are saved.
    /// </summary>
    [JsonIgnore]
    public bool HasUnsavedChanges { get; protected set; }

    /// <summary>
    /// Runs after a settings object is loaded or created.
    /// </summary>
    /// <param name="fromFile">
    /// <see langword="true"/> when the settings object was deserialized from disk; otherwise,
    /// <see langword="false"/>.
    /// </param>
    protected internal void OnLoadedCore(bool fromFile)
    {
        OnLoaded(fromFile);
        IsLoaded = true;
        ClearUnsavedChangesCore();
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

    /// <summary>
    /// Clears the dirty flag after the settings object has been persisted.
    /// </summary>
    protected internal virtual void ClearUnsavedChangesCore()
    {
        HasUnsavedChanges = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        HasUnsavedChanges = true;
        base.OnPropertyChanged(e);
    }
}

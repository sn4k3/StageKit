namespace StageKit.Interfaces;

/// <summary>
/// Represents an object that can be saved.
/// </summary>
public interface ISavable
{
    /// <summary>
    /// Persists the current state or data to the underlying storage.
    /// </summary>
    /// <remarks>Call this method to ensure that any changes made are saved. The specific storage mechanism
    /// and persistence behavior depend on the implementation.</remarks>
    public void Save();
}
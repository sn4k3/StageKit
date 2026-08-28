using System.Runtime.InteropServices;

namespace StageKit.Primitives;

/// <summary>
/// Base class for disposable objects that directly own unmanaged resources and require finalizer fallback.
/// </summary>
/// <remarks>
/// Prefer <see cref="SafeHandle"/> for native handles. Managed-only types should derive
/// from <see cref="DisposableObject"/> so they do not incur finalizer-queue overhead.
/// </remarks>
public abstract class UnmanagedDisposableObject : DisposableObject
{
    /// <summary>
    /// Finalizes the object and releases its unmanaged resources.
    /// </summary>
    ~UnmanagedDisposableObject()
    {
        Dispose(false);
    }
}

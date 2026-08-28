/*
 *   MIT License
 *
 *   Copyright (c) 2026 Tiago Conceição
 *
 *   Permission is hereby granted, free of charge, to any person obtaining a copy
 *   of this software and associated documentation files (the "Software"), to deal
 *   in the Software without restriction, including without limitation the rights
 *   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *   copies of the Software, and to permit persons to whom the Software is
 *   furnished to do so, subject to the following conditions:
 *
 *   The above copyright notice and this permission notice shall be included in all
 *   copies or substantial portions of the Software.
 *
 *   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *   SOFTWARE.
 */

using System.Runtime.InteropServices;

namespace StageKit.Primitives;

/// <summary>
/// Base class for objects that require deterministic cleanup of resources, both managed and unmanaged.
/// </summary>
public abstract class DisposableObject : IDisposable
{
    /// <summary>
    /// Indicates whether the object has already been disposed of. Used to prevent multiple disposal attempts.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Releases all resources used by the object.
    /// </summary>
    /// <remarks>Call this method when the object is no longer needed to free unmanaged resources
    /// deterministically. After calling this method, the object should not be used. This method suppresses finalization
    /// to prevent the garbage collector from calling the finalizer.</remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Checks if the object has been disposed of and throws an <see cref="ObjectDisposedException"/> if it has.
    /// This method should be called at the beginning of any public method that accesses resources to ensure that the object is still valid.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, GetType());
    }

    /// <summary>
    /// Releases all resources used by the object. If disposing is true, both managed and unmanaged resources are released.
    /// </summary>
    /// <param name="disposing">True when called by <see cref="Dispose()"/>; false when called by a finalizer.</param>
    protected void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            if (disposing)
            {
                // Dispose managed resources if called from Dispose()
                DisposeManaged();
            }
        }
        finally
        {
            // Always dispose the unmanaged resources
            DisposeUnmanaged();
        }
    }

    /// <summary>
    /// Releases all resources used by the managed objects associated with this instance.
    /// </summary>
    /// <remarks>This method should be called when the managed resources are no longer needed to ensure proper
    /// cleanup. It is recommended to call this method in the implementation of the Dispose pattern.</remarks>
    protected abstract void DisposeManaged();

    /// <summary>
    /// Releases unmanaged resources used by the object during explicit disposal.
    /// </summary>
    /// <remarks>Use <see cref="UnmanagedDisposableObject"/> when a derived type also requires finalizer-based
    /// cleanup. Prefer <see cref="SafeHandle"/> for native handles.</remarks>
    protected virtual void DisposeUnmanaged()
    {
    }
}
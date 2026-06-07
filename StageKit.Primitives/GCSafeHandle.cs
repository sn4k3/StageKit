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
/// A SafeHandle wrapper for GCHandle to ensure pinned memory is released correctly.
/// This allows passing the object directly to P/Invoke methods that expect an IntPtr (if pinned).
/// </summary>
// ReSharper disable once InconsistentNaming
public class GCSafeHandle : SafeHandle
{
    /// <summary>
    /// The GCHandle that this SafeHandle manages. It can be of any type (Pinned, Normal, Weak, etc.) depending on the use case.
    /// </summary>
    private GCHandle _gcHandle;

    /// <summary>
    /// Allocates a GCHandle for the specified object.
    /// </summary>
    /// <param name="dataObject">The object to pin or reference.</param>
    /// <param name="handleType">Type of handle to create (default is Pinned).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataObject"/> is null.</exception>
    public GCSafeHandle(object dataObject, GCHandleType handleType = GCHandleType.Pinned)
        : base(IntPtr.Zero, true)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        _gcHandle = GCHandle.Alloc(dataObject, handleType);

        // If pinned, we can expose the address as the dangerous handle
        if (handleType == GCHandleType.Pinned)
        {
            SetHandle(_gcHandle.AddrOfPinnedObject());
        }
    }

    /// <summary>
    /// Gets a value indicating whether the handle is invalid.
    /// </summary>
    public override bool IsInvalid => !_gcHandle.IsAllocated;

    /// <summary>
    /// Releases the GCHandle.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }

        return true;
    }
}
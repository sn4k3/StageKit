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

using System.Buffers;
using System.Runtime.CompilerServices;

namespace StageKit.Primitives;

/// <summary>
/// A <see cref="MemoryManager{T}"/> implementation that wraps a block of already-pinned, unmanaged memory
/// and exposes it as a <see cref="Memory{T}"/>.
/// </summary>
/// <remarks>
/// The wrapped pointer is assumed to reference memory that is fixed for the lifetime of the produced
/// <see cref="Memory{T}"/>; therefore <see cref="Pin"/> is a no-op other than returning the requested address
/// and the manager owns no resources to release. The caller is responsible for ensuring the underlying buffer
/// outlives any <see cref="Memory{T}"/> obtained from this manager, exactly as with the span accessors.
/// Each instance is a small heap allocation, which is the unavoidable cost of obtaining a <see cref="Memory{T}"/>
/// over unmanaged memory; prefer the span accessors when allocation-free access is sufficient.
/// </remarks>
/// <typeparam name="T">The unmanaged type of the elements in the memory block.</typeparam>
public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private readonly int _length;
    private readonly void* _pointer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnmanagedMemoryManager{T}"/> class.
    /// </summary>
    /// <param name="pointer">The address of the first element of the unmanaged memory block.</param>
    /// <param name="length">The number of <typeparamref name="T"/> elements in the block.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pointer"/> is zero (null) for a non-empty block,
    /// or when <paramref name="length"/> is negative.
    /// </exception>
    public UnmanagedMemoryManager(void* pointer, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (pointer is null && length != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointer), "Pointer must not be null for a non-empty block.");
        }

        _pointer = pointer;
        _length = length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnmanagedMemoryManager{T}"/> class.
    /// </summary>
    /// <param name="pointer">The address of the first element of the unmanaged memory block.</param>
    /// <param name="length">The number of <typeparamref name="T"/> elements in the block.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pointer"/> is zero (null) for a non-empty block,
    /// or when <paramref name="length"/> is negative.
    /// </exception>
    public UnmanagedMemoryManager(nint pointer, int length) : this((void*)pointer, length)
    {
    }

    /// <inheritdoc/>
    public override Span<T> GetSpan()
    {
        return new Span<T>(_pointer, _length);
    }

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, _length);

        var byteOffset = (long)elementIndex * Unsafe.SizeOf<T>();
        return new MemoryHandle((byte*)_pointer + byteOffset, pinnable: this);
    }

    /// <inheritdoc/>
    public override void Unpin()
    {
        // The underlying memory is assumed to be permanently pinned; nothing to do.
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // This manager does not own the memory it wraps; nothing to release.
    }
}
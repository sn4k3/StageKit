namespace StageKit.Primitives;

/// <summary>
/// An abstract disposable base class that supports a leave-open semantic, allowing the caller to retain
/// ownership of the underlying resource after disposal when <see cref="LeaveOpen"/> is set to <see langword="true"/>.
/// </summary>
public abstract class LeaveOpenDisposableObject : DisposableObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the source object should remain open after disposal.<br />
    /// When set to true, the object will not be disposed when the Dispose method is called,
    /// allowing it to be reused or managed externally.<br />
    /// When set to false, the object will be disposed as usual when Dispose is called.
    /// This property is useful for scenarios where the lifecycle of the object needs to be
    /// controlled externally, such as when sharing resources across multiple components or when
    /// managing resources that should not be automatically cleaned up by the class itself.
    /// </summary>
    public bool LeaveOpen { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaveOpenDisposableObject"/> class with the default setting for leaving the source object open (false).
    /// </summary>
    protected LeaveOpenDisposableObject()
    {
    }

    /// <summary>
    /// Initializes a new instance of the LeaveOpenDisposableObject class with the specified leave-open behavior.
    /// </summary>
    /// <remarks>Use this constructor to control whether the underlying resource should remain open after
    /// disposing the object. This is useful when the resource is shared or managed externally.</remarks>
    /// <param name="leaveOpen">true to leave the underlying resource open when the object is disposed; otherwise, false.</param>
    protected LeaveOpenDisposableObject(bool leaveOpen)
    {
        LeaveOpen = leaveOpen;
    }
}
namespace StageKit.Fallout;

/// <summary>
/// Specifies the role of the application with respect to a document type or file association.
/// </summary>
public enum DocumentRole
{
    /// <summary>
    /// The application can read, edit, and save documents of this type.
    /// </summary>
    Editor,

    /// <summary>
    /// The application can read and display documents of this type, but cannot save changes.
    /// </summary>
    Viewer,

    /// <summary>
    /// The application provides services for other applications to use with documents of this type.
    /// </summary>
    Shell,

    /// <summary>
    /// The application generates Quick Look previews for documents of this type.
    /// </summary>
    QuickLookGenerator,

    /// <summary>
    /// The application does not open documents of this type directly, but declares its existence.
    /// </summary>
    None
}

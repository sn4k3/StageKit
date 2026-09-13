namespace StageKit.Fallout;

/// <summary>
/// Specifies the handler rank for a file association or document type on macOS.
/// </summary>
public enum BundleHandlerRank
{
    /// <summary>
    /// The application is the default handler for the document type.
    /// </summary>
    Default,

    /// <summary>
    /// The application is the creator and primary owner of the document type.
    /// </summary>
    Owner,

    /// <summary>
    /// The application is a secondary handler for the document type.
    /// </summary>
    Alternate,

    /// <summary>
    /// The application does not handle opening documents of this type.
    /// </summary>
    None
}

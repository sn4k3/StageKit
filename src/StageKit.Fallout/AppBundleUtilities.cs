namespace StageKit.Fallout;

/// <summary>
/// Provides utility methods for handling application bundles, including string escaping and quoting for various contexts such as Bash, YAML, and shell commands, as well as validation of required string values.
/// </summary>
public static class AppBundleUtilities
{
    /// <summary>
    /// Validates that a string value is not null, empty, or whitespace. Throws an ArgumentException if the validation fails.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <exception cref="ArgumentException">Thrown if the value is null, empty, or whitespace.</exception>
    public static void ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be null, empty, or white space.", parameterName);
        }
    }
}
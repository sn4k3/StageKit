using StageKit.Primitives;

namespace StageKit.Tests;

public sealed class FileUtilitiesTests
{
    [Fact]
    public void SanitizeFileName_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => FileUtilities.SanitizeFileName(null!));
    }

    [Fact]
    public void SanitizeFileName_ReturnsSameInstance_WhenNoInvalidChars()
    {
        const string valid = "valid-file_name.123.txt";
        var result = FileUtilities.SanitizeFileName(valid);

        Assert.Same(valid, result);
    }

    [Fact]
    public void SanitizeFileName_ReturnsSameInstance_WhenEmpty()
    {
        var result = FileUtilities.SanitizeFileName(string.Empty);

        Assert.Same(string.Empty, result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidCharacters()
    {
        var invalid = Path.GetInvalidFileNameChars();
        var input = $"prefix{invalid[0]}middle{invalid[^1]}suffix.txt";
        var expected = "prefix_middle_suffix.txt";

        var result = FileUtilities.SanitizeFileName(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesConsecutiveInvalidCharacters()
    {
        var invalid = Path.GetInvalidFileNameChars();
        var input = $"a{invalid[0]}{invalid[0]}b";
        var expected = "a__b";

        var result = FileUtilities.SanitizeFileName(input);

        Assert.Equal(expected, result);
    }
}

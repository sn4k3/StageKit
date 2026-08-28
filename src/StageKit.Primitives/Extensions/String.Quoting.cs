namespace StageKit.Primitives.Extensions;

/// <summary>
/// Provides extension methods for quoting and escaping strings for various contexts, including Bash, YAML, shell commands, and process arguments.
/// </summary>
public static class StringExtensions
{
    extension(string value)
    {
        /// <summary>
        /// Escapes a string for use in a Bash double-quoted context, handling special characters such as backslashes, double quotes, dollar signs, and backticks.
        /// </summary>
        /// <returns>The escaped string.</returns>
        public string EscapeBashDoubleQuoted()
        {
            var source = value;
            var escapeCount = 0;

            foreach (var c in source)
            {
                if (c is '\\' or '"' or '$' or '`')
                    escapeCount++;
            }

            if (escapeCount == 0)
                return source;

            return string.Create(
                source.Length + escapeCount,
                source,
                static (destination, source) =>
                {
                    var i = 0;

                    foreach (var c in source)
                    {
                        if (c is '\\' or '"' or '$' or '`')
                            destination[i++] = '\\';

                        destination[i++] = c;
                    }
                });
        }

        /// <summary>
        /// Quotes a string for use in a YAML context, escaping single quotes by doubling them.
        /// </summary>
        /// <returns>The quoted string.</returns>
        public string QuoteYaml()
        {
            return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
        }

        /// <summary>
        /// Quotes a string for use in a shell context, escaping single quotes by closing the quote, adding an escaped single quote, and reopening the quote.
        /// </summary>
        /// <returns>The quoted string.</returns>
        public string QuoteShell()
        {
            return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
        }

        /// <summary>
        /// Quotes a string for use as a single argument in a process argument line, using the escaping rules the
        /// .NET process launcher applies (double quotes, with backslashes doubled before a quote).
        /// </summary>
        /// <returns>The quoted argument.</returns>
        public string QuoteProcessArgument()
        {
            var extraLength = 2; // Opening + closing quotes.
            var backslashes = 0;

            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        backslashes++;
                        break;

                    case '"':
                        // Backslashes preceding a quote are doubled,
                        // plus the quote itself needs an escape.
                        extraLength += backslashes + 1;
                        backslashes = 0;
                        break;

                    default:
                        backslashes = 0;
                        break;
                }
            }

            // Trailing backslashes must be doubled before closing quote.
            extraLength += backslashes;

            return string.Create(
                value.Length + extraLength,
                value,
                static (destination, source) =>
                {
                    var index = 0;
                    var backslashes = 0;

                    destination[index++] = '"';

                    foreach (var c in source)
                    {
                        switch (c)
                        {
                            case '\\':
                                backslashes++;
                                break;

                            case '"':
                                destination.Slice(index, backslashes * 2 + 1).Fill('\\');
                                index += backslashes * 2 + 1;
                                destination[index++] = '"';
                                backslashes = 0;
                                break;

                            default:
                                destination.Slice(index, backslashes).Fill('\\');
                                index += backslashes;
                                destination[index++] = c;
                                backslashes = 0;
                                break;
                        }
                    }

                    destination.Slice(index, backslashes * 2).Fill('\\');
                    index += backslashes * 2;

                    destination[index] = '"';
                });
        }
    }
}
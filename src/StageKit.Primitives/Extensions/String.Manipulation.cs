namespace StageKit.Primitives.Extensions;

public static partial class StringExtensions
{
    /// <param name="str">The string to parse</param>
    extension(string str)
    {
        /// <summary>
        /// Parse the input string by placing a space between character case changes in the string
        /// </summary>
        /// <param name="insertChar">The character to insert between case (and, optionally, digit) transitions.</param>
        /// <param name="splitNumbers">Also split numbers with a whitespace</param>
        /// <returns>The altered string</returns>
        public string InsertCharBetweenCamelCase(char insertChar = ' ', bool splitNumbers = true)
        {
            if (string.IsNullOrWhiteSpace(str)) return str;

            var lastCharPos = str.Length - 1;
            var insertions = 0;

            for (var i = 1; i <= lastCharPos; i++)
            {
                if (RequiresInsertBefore(str[i], str[i - 1], i == lastCharPos, splitNumbers)) insertions++;
            }

            if (insertions == 0) return str;

            return string.Create(
                str.Length + insertions,
                (str, insertChar, splitNumbers),
                static (destination, state) =>
                {
                    var (source, separator, splitDigits) = state;
                    var last = source.Length - 1;
                    var index = 0;

                    destination[index++] = source[0];

                    for (var i = 1; i <= last; i++)
                    {
                        if (RequiresInsertBefore(source[i], source[i - 1], i == last, splitDigits))
                        {
                            destination[index++] = separator;
                        }

                        destination[index++] = source[i];
                    }
                });
        }
    }

    private static bool RequiresInsertBefore(char current, char previous, bool isLast, bool splitNumbers)
    {
        return (char.IsUpper(current) && char.IsLower(previous))
               || (splitNumbers && !isLast && char.IsNumber(current) && !char.IsNumber(previous));
    }
}
namespace StageKit.Extensions;

/// <summary>
/// Provides extension methods for enumerating exceptions and their inner exceptions in different ways.
/// </summary>
public static class ExceptionExtensions
{
    extension(Exception e)
    {
        /// <summary>
        /// Enumerates an exception and its inner exceptions using the specified traversal type.
        /// </summary>
        /// <param name="traversalType">The type of exception traversal to perform.</param>
        /// <returns>A sequence containing the supplied exception followed by its traversed inner exceptions.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="traversalType"/> is not supported.</exception>
        public IEnumerable<Exception> EnumerateExceptions(
            ExceptionTraversalType traversalType = ExceptionTraversalType.ExceptionTree)
        {
            if (traversalType is not ExceptionTraversalType.ExceptionTree and
                not ExceptionTraversalType.InnerExceptionChain)
            {
                throw new ArgumentOutOfRangeException(nameof(traversalType), traversalType, null);
            }

            var currentException = e;
            Stack<Exception>? pendingExceptions = null;

            while (true)
            {
                yield return currentException;

                if (traversalType is ExceptionTraversalType.ExceptionTree &&
                    currentException is AggregateException { InnerExceptions.Count: > 0 } aggregateException)
                {
                    for (var i = aggregateException.InnerExceptions.Count - 1; i >= 1; i--)
                    {
                        (pendingExceptions ??= new Stack<Exception>()).Push(aggregateException.InnerExceptions[i]);
                    }

                    currentException = aggregateException.InnerExceptions[0];
                    continue;
                }

                if (currentException.InnerException is not null)
                {
                    currentException = currentException.InnerException;
                    continue;
                }

                if (pendingExceptions is null || !pendingExceptions.TryPop(out currentException))
                {
                    yield break;
                }
            }
        }
    }
}

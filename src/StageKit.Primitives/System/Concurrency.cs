namespace StageKit.Primitives.System;

/// <summary>
/// Provides concurrency-related utilities.
/// </summary>
public static class Concurrency
{
    /// <summary>
    /// Gets the ParallelOptions with <c>MaxDegreeOfParallelism</c> set to 1 for debug purposes
    /// </summary>
    public static ParallelOptions DebugParallelOptions => new() { MaxDegreeOfParallelism = 1 };

    /// <summary>
    /// Gets or sets the optimal maximum degree of parallelism.
    /// -1 means use .net default, 0 means use <see cref="Environment.ProcessorCount"/>, -2 means auto-detect based on processor count less a few cores for responsiveness.
    /// </summary>
    /// <remarks>Default: -2</remarks>
    public static int OptimalMaxDegreeOfParallelism
    {
        get
        {
            if (field <= -2)
            {
                var processorCount = Environment.ProcessorCount;
                field = processorCount switch
                {
                    //>= 24 => processorCount - 6,
                    >= 20 => processorCount - 5,
                    >= 16 => processorCount - 4,
                    >= 12 => processorCount - 3,
                    >= 8 => processorCount - 2,
                    >= 4 => processorCount - 1,
                    _ => processorCount,
                };
            }
            return field;
        }
        set
        {
            if (value == 0)
            {
                field = Environment.ProcessorCount;
            }
            else
            {
                field = Math.Min(value, Environment.ProcessorCount);
            }
        }
    } = -2;

    /// <summary>Options for <see cref="Parallel"/> loops, capped at <see cref="OptimalMaxDegreeOfParallelism"/>.</summary>
    public static ParallelOptions ParallelOptions =>
        new() { MaxDegreeOfParallelism = OptimalMaxDegreeOfParallelism };

    /// <summary>
    /// Gets the ParallelOptions with <see cref="OptimalMaxDegreeOfParallelism"/> and the <see cref="CancellationToken"/> set
    /// </summary>
    public static ParallelOptions GetParallelOptions(CancellationToken token = default)
    {
        var options = ParallelOptions;
        options.CancellationToken = token;
        return options;
    }

    /// <summary>
    /// Caps a PLINQ query at <see cref="OptimalMaxDegreeOfParallelism"/>. Apply after
    /// <c>AsParallel().AsOrdered()</c>: PLINQ only accepts <c>AsOrdered</c> directly on <c>AsParallel</c>.
    /// </summary>
    public static ParallelQuery<T> WithOptimalParallelism<T>(this ParallelQuery<T> query)
    {
        return query.WithDegreeOfParallelism(OptimalMaxDegreeOfParallelism);
    }
}

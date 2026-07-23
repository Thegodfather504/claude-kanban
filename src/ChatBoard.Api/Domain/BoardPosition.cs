namespace ChatBoard.Api.Domain;

/// <summary>
/// The single authoritative source for sort-key arithmetic. Cards carry a
/// <see cref="double"/> position rather than a contiguous index so a drag-and-drop
/// writes exactly one row. Both create (append) and move (reorder) go through here.
/// </summary>
public static class BoardPosition
{
    /// <summary>Default spacing between neighbours, and the position of the first card in a column.</summary>
    public const double Gap = 1024d;

    /// <summary>When two neighbours are closer than this, the column can no longer be split cleanly and needs a rebalance.</summary>
    public const double RebalanceThreshold = 0.0001d;

    /// <summary>Pick a position between two optional neighbours (either may be null at a column edge).</summary>
    public static double Between(double? above, double? below) => (above, below) switch
    {
        (null, null)   => Gap,
        (null, { } b)  => b - Gap,
        ({ } a, null)  => a + Gap,
        ({ } a, { } b) => a + ((b - a) / 2d)
    };

    /// <summary>True when neighbours have converged too closely to place a card between them.</summary>
    public static bool NeedsRebalance(double above, double below) =>
        Math.Abs(below - above) < RebalanceThreshold;

    /// <summary>Evenly spaced positions for renumbering a whole column of <paramref name="count"/> cards.</summary>
    public static IEnumerable<double> Rebalanced(int count) =>
        Enumerable.Range(1, count).Select(i => i * Gap);
}

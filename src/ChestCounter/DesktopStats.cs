namespace ChestCounter;

public sealed record HistoryRow(DateTime Date, int Chests, int? Placement, string Mode);

// Immutable display snapshot: the UI never enumerates the tracker's mutable history.
public sealed record DesktopStats(long Total, int Initial, long SessionChests, int SessionMatches,
    double Average, IReadOnlyList<HistoryRow> History)
{
    public static readonly DesktopStats Empty = new(0, 0, 0, 0, 0, Array.Empty<HistoryRow>());

    public static DesktopStats Build(IEnumerable<MatchRecord> source, MatchRecord? live, int initial)
    {
        var matches = source.ToList();
        var session = matches.Where(ReportWriter.IsThisSession).ToList();
        var counted = matches.Sum(m => (long)m.ChestsOpened);
        return new(counted + (live?.ChestsOpened ?? 0) + Math.Max(0, initial), Math.Max(0, initial),
            session.Sum(m => (long)m.ChestsOpened) + (live?.ChestsOpened ?? 0), session.Count,
            matches.Count == 0 ? 0 : (double)counted / matches.Count,
            matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc)
                .Select(m => new HistoryRow((m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime(),
                    m.ChestsOpened, m.Placement, m.Playlist ?? "—")).ToArray());
    }
}

public static class DropMath
{
    public static double Probability(int oneIn, long chests)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(oneIn, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(chests);
        if (chests == 0) return 0;
        if (oneIn == 1) return 1;
        return 1 - Math.Pow(1 - 1d / oneIn, chests);
    }

    public static long ChestsFor(int oneIn, double probability)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(oneIn, 1);
        if (!double.IsFinite(probability) || probability <= 0 || probability >= 1)
            throw new ArgumentOutOfRangeException(nameof(probability));
        return oneIn == 1 ? 1 : (long)Math.Ceiling(Math.Log(1 - probability) / Math.Log(1 - 1d / oneIn));
    }
}

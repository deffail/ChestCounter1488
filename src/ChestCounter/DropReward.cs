namespace ChestCounter;

// User-supplied odds for ordinary matches, per chest. No online lookup or automatic refresh.
public sealed record DropReward(string Id, string Ru, string En, int OneIn, bool Repeatable = false)
{
    public override string ToString() => L.T(Ru, En);
    public static readonly DropReward[] All =
    {
        new("outfit", "Золотой скин", "Weekly Gold Outfit", 1562500),
        new("shop", "Пиратский магазин", "Pirate Shop Access", 137500),
        new("vehicle", "Транспорт и декаль", "Weekly Vehicle and Decal", 7813),
        new("pickaxe", "Кирка / украшение", "Weekly Pickaxe or Back Bling", 568),
        new("track", "Джем-трек", "Weekly Jam Track", 391),
        new("loading", "Экран загрузки", "Weekly Loading Screen", 272),
        new("gizmo", "Любой гизмо (повторяемый)", "Any Gizmo (repeatable)", 127, true),
        new("custom", "Свой шанс (повторяемый)", "Custom odds (repeatable)", 2000, true),
    };

    public double ExpectedRewards(int oneIn, long chests, bool claimed) =>
        Repeatable ? (double)chests / oneIn : claimed ? 0 : DropMath.Probability(oneIn, chests);
}

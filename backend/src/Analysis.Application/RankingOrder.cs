namespace Analysis.Application;

public static class RankingOrder
{
    public static IOrderedEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T, decimal?> composite, Func<T, string> assetId) =>
        items.OrderBy(i => composite(i) is null).ThenByDescending(composite).ThenBy(assetId, StringComparer.Ordinal);
}

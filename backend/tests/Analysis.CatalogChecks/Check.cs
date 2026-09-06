namespace Analysis.CatalogChecks;

internal static class Check
{
    public static readonly List<string> Passed = [];
    public static void That(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    public static void Pass(string label) { Passed.Add(label); Console.WriteLine($"PASS {label}"); }
    public static void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}: {label}");
    }
    public static async Task ThrowsAsync<T>(Func<Task> action, string label) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}: {label}");
    }
}

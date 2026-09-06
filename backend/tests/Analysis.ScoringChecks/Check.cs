namespace Analysis.ScoringChecks;

internal static class Check
{
    public static int Count { get; private set; }
    public static void That(bool condition, string message)
    { Count++; if (!condition) throw new InvalidOperationException(message); }
    public static void Equal<T>(T expected, T actual, string message) => That(EqualityComparer<T>.Default.Equals(expected, actual), message);
    public static void Throws<T>(Action action, string message) where T : Exception
    { try { action(); } catch (T) { Count++; return; } throw new InvalidOperationException(message); }
    public static async Task ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    { try { await action(); } catch (T) { Count++; return; } throw new InvalidOperationException(message); }
    public static void Pass(string text) => Console.WriteLine($"PASS {text}");
}

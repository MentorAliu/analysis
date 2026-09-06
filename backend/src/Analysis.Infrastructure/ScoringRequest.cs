using System.Globalization;
using Analysis.Application;

namespace Analysis.Infrastructure;

public sealed record ScoringCommand(ScoreRequest? Score, string ModelId, DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public const string Usage = "--score-once --private-use --country XK --as-of-utc T --knowledge-cutoff-utc K --model slice1-v1 OR --replay-scores --model slice1-v1 --start-utc A --end-utc B";
    public static bool TryParse(string[] args, DateTimeOffset now, out ScoringCommand? command)
    {
        command = null;
        if (args is ["--score-once", "--private-use", "--country", "XK", "--as-of-utc", var asOfText,
            "--knowledge-cutoff-utc", var cutoffText, "--model", "slice1-v1"] &&
            Parse(asOfText, out var asOf) && Parse(cutoffText, out var cutoff) && Hour(asOf) && cutoff >= asOf && cutoff <= now)
        { command = new(new(asOf, cutoff, "slice1-v1"), "slice1-v1", default, default); return true; }
        if (args is ["--replay-scores", "--model", "slice1-v1", "--start-utc", var startText, "--end-utc", var endText] &&
            Parse(startText, out var start) && Parse(endText, out var end) && Hour(start) && Hour(end) &&
            end > start && end <= now && end - start <= TimeSpan.FromDays(7))
        { command = new(null, "slice1-v1", start, end); return true; }
        return false;
    }
    private static bool Parse(string value, out DateTimeOffset time) => DateTimeOffset.TryParseExact(value,
        "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);
    private static bool Hour(DateTimeOffset time) => time.ToUnixTimeMilliseconds() % 3_600_000 == 0;
}

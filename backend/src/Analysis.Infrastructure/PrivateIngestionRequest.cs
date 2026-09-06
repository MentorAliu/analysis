using System.Globalization;
using Analysis.Domain;

namespace Analysis.Infrastructure;

public sealed record PrivateIngestionRequest(ReadWindow Window)
{
    public const string Usage = "--ingest-once --private-use --country XK --start-utc yyyy-MM-ddTHH:mm:ssZ --end-utc yyyy-MM-ddTHH:mm:ssZ";

    // Parse before building a host or reading configuration: malformed commands do no I/O.
    public static bool TryParse(string[] args, DateTimeOffset now, out PrivateIngestionRequest? request)
    {
        request = null;
        if (args is not ["--ingest-once", "--private-use", "--country", "XK", "--start-utc", var startText, "--end-utc", var endText])
            return false;
        const string format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (!DateTimeOffset.TryParseExact(startText, format, CultureInfo.InvariantCulture, styles, out var start) ||
            !DateTimeOffset.TryParseExact(endText, format, CultureInfo.InvariantCulture, styles, out var end) ||
            end <= start || end > now || end - start > TimeSpan.FromDays(7) ||
            start.Minute != 0 || start.Second != 0 || end.Minute != 0 || end.Second != 0)
            return false;
        request = new(new ReadWindow(start, end));
        return true;
    }
}

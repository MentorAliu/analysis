using System.Globalization;
using System.Text.RegularExpressions;
using Analysis.Application;
using Analysis.Domain;

namespace Analysis.Infrastructure;

public sealed record ForwardCommand(string Operation, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? AfterId)
{
    public static readonly string[] Operations = ["--collect-forward-inputs-once", "--issue-forward-once",
        "--collect-outcome-prices-once", "--measure-outcomes-once", "--inspect-forward-records"];
    public const string Usage = "--collect-forward-inputs-once --private-use --country XK --as-of-utc <UTC-hour> | --issue-forward-once --private-use --country XK --as-of-utc <UTC-hour> --model slice1-v1 | --collect-outcome-prices-once --private-use --country XK --start-utc <UTC-hour> --end-utc <UTC-hour> | --measure-outcomes-once --private-use --country XK --start-utc <UTC> --end-utc <UTC> [--after <issuance-id>] | --inspect-forward-records --private-use --country XK --start-utc <UTC> --end-utc <UTC>";
    public bool Acquisition => Operation is "--collect-forward-inputs-once" or "--collect-outcome-prices-once";
    public ForwardRange Range => new(StartUtc, EndUtc, AfterId);
    public static bool TryParse(string[] args, out ForwardCommand? command)
    {
        command = null;
        if (args.Length < 6 || !Operations.Contains(args[0], StringComparer.Ordinal) || args[1] != "--private-use" || args[2] != "--country" || args[3] != "XK") return false;
        var operation = args[0];
        if (operation is "--collect-forward-inputs-once" or "--issue-forward-once")
        {
            if (args[4] != "--as-of-utc" || !Parse(args[5], out var t) || t.Ticks % TimeSpan.TicksPerHour != 0) return false;
            if (operation == "--collect-forward-inputs-once" ? args.Length != 6 : args is not [_, _, _, _, _, _, "--model", "slice1-v1"]) return false;
            if (t > DateTimeOffset.MaxValue.AddHours(-1)) return false;
            command = new(operation, t, t.AddHours(1), null); return true;
        }
        if (args.Length < 8 || args[4] != "--start-utc" || args[6] != "--end-utc" || !Parse(args[5], out var start) || !Parse(args[7], out var end) ||
            end <= start || end - start > TimeSpan.FromDays(7)) return false;
        if (operation == "--collect-outcome-prices-once" && (start.Ticks % TimeSpan.TicksPerHour != 0 || end.Ticks % TimeSpan.TicksPerHour != 0)) return false;
        string? after = null;
        if (args.Length != 8)
        {
            if (operation != "--measure-outcomes-once" || args.Length != 10 || args[8] != "--after" || !Regex.IsMatch(args[9], "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant)) return false;
            after = args[9];
        }
        command = new(operation, start, end, after); return true;
    }
    private static bool Parse(string value, out DateTimeOffset utc) => DateTimeOffset.TryParseExact(value,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"], CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
}

using Microsoft.Extensions.Logging;

namespace Analysis.Hosting;

public static class StructuredLoggingExtensions
{
    public static ILoggingBuilder AddPlatformJsonConsole(
        this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            options.UseUtcTimestamp = true;
        });

        return logging;
    }
}

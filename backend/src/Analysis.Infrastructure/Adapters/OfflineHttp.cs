namespace Analysis.Infrastructure.Adapters;

// Offline checks retain a transport that cannot target a live provider.
public sealed class OfflineHttp : IProviderHttp, IDisposable
{
    private readonly JsonHttp http;

    public OfflineHttp(Uri fixtureServer)
    {
        if (!fixtureServer.IsAbsoluteUri || !fixtureServer.IsLoopback || fixtureServer.Scheme != "http" ||
            fixtureServer.UserInfo.Length != 0 || fixtureServer.Query.Length != 0 ||
            fixtureServer.Fragment.Length != 0 || fixtureServer.AbsolutePath != "/")
            throw new ArgumentException("Only an HTTP loopback fixture server is permitted.");
        http = new(fixtureServer);
    }

    public Task<byte[]> GetAsync(string path, CancellationToken cancellationToken) => http.GetAsync(path, cancellationToken);
    public void Dispose() => http.Dispose();
}

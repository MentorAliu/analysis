namespace Analysis.Infrastructure.Adapters;

// Infrastructure-local transport boundary; no provider contract enters the domain.
public interface IProviderHttp
{
    Task<byte[]> GetAsync(string path, CancellationToken cancellationToken);
}

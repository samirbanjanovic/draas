using DRaaS.CoreLib.Models;

namespace DRaaS.CoreLib.Providers.Impl;

public class PlatformProviderRegistry : IPlatformProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IPlatformProvider> _providers;
    private readonly IPlatformProvider _defaultProvider;

    public PlatformProviderRegistry(IEnumerable<IPlatformProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var providerList = providers.ToList();

        if (providerList.Count == 0)
            throw new InvalidOperationException(
                "No platform providers registered. Register at least one provider (e.g., AddDockerInstanceProvider()).");

        _providers = providerList.ToDictionary(
            p => p.PlatformType,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        _defaultProvider = providerList.FirstOrDefault(p => p.IsAvailable)
            ?? providerList[0];
    }

    public IPlatformProvider DefaultProvider => _defaultProvider;

    public Task<IPlatformProvider> GetProviderAsync(
        string platformType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformType);

        if (!_providers.TryGetValue(platformType, out var provider))
        {
            throw new InvalidOperationException(
                $"Platform provider '{platformType}' not found. Available providers: {string.Join(", ", _providers.Keys)}");
        }

        return Task.FromResult(provider);
    }

    public Task<IEnumerable<RegisteredPlatform>> GetProvidersAsync(
        bool availableOnly = false,
        CancellationToken cancellationToken = default)
    {
        var providers = _providers.Values;

        if (availableOnly)
        {
            providers = providers.Where(p => p.IsAvailable).ToList();
        }

        var registered = providers.Select(p =>
            new RegisteredPlatform(
                IsDefault: p == _defaultProvider,
                Info: p.GetPlatformInfo()
            ));

        return Task.FromResult(registered);
    }
}

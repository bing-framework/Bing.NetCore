using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Resilience.Operations;

/// <summary>不可变宿主操作目录。写重放许可与只读许可分别登记。</summary>
public sealed class ResilienceOperationCatalog
{
    private readonly IReadOnlyDictionary<string, ResilienceOperationDescriptor> _operations;
    public ResilienceOperationCatalog(IEnumerable<ResilienceOperationDescriptor> operations)
    {
        try { _operations = operations.ToDictionary(x => x.Name, StringComparer.Ordinal); }
        catch (ArgumentException) { throw new ResilienceConfigurationException("操作名重复登记。"); }
    }
    public ResilienceOperationDescriptor Get(string name) => _operations.TryGetValue(name, out var operation)
        ? operation : throw new ResilienceConfigurationException($"未许可操作 '{name}'。");
}
public static class ResilienceOperationExtensions
{
    public static IServiceCollection AddBingResilienceOperations(this IServiceCollection services, params ResilienceOperationDescriptor[] operations)
    {
        if (services.Any(x => x.ServiceType == typeof(ResilienceOperationCatalog))) throw new ResilienceConfigurationException("操作目录已登记。");
        services.AddSingleton(new ResilienceOperationCatalog(operations));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IdempotencyCoordinator>();
        return services;
    }
}

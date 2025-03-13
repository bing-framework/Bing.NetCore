using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.MultiTenancy.ConfigurationStore;

/// <summary>
/// 默认租户存储器
/// </summary>
[Dependency(ServiceLifetime.Transient, TryAdd = true)]
public class DefaultTenantStore : ITenantStore
{
    /// <summary>
    /// 默认租户存储选项配置
    /// </summary>
    private readonly BingDefaultTenantStoreOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultTenantStore"/>类型的实例
    /// </summary>
    /// <param name="options">默认租户存储选项配置</param>
    public DefaultTenantStore(IOptionsMonitor<BingDefaultTenantStoreOptions> options) => _options = options.CurrentValue;

    /// <inheritdoc />
    public Task<TenantConfiguration?> FindByNameAsync(string normalizedName) => Task.FromResult(_options.Tenants?.FirstOrDefault(x => x.NormalizedName == normalizedName));

    /// <inheritdoc />
    public Task<TenantConfiguration?> FindByIdAsync(string id) => Task.FromResult(_options.Tenants?.FirstOrDefault(x => x.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeDetails = false) => Task.FromResult<IReadOnlyList<TenantConfiguration>>(_options.Tenants);
}

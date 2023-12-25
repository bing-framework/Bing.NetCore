using Bing.DependencyInjection;
using Bing.MultiTenancy.Localization;
using Microsoft.Extensions.Localization;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户配置提供程序
/// </summary>
public class TenantConfigurationProvider : ITenantConfigurationProvider, ITransientDependency
{
    /// <summary>
    /// 初始化一个<see cref="TenantConfigurationProvider"/>类型的实例
    /// </summary>
    /// <param name="tenantResolver">租户解析器</param>
    /// <param name="tenantStore">租户存储器</param>
    /// <param name="tenantResolveResultAccessor">租户解析结果访问器</param>
    /// <param name="stringLocalizer">国际化</param>
    public TenantConfigurationProvider(
        ITenantResolver tenantResolver,
        ITenantStore tenantStore,
        ITenantResolveResultAccessor tenantResolveResultAccessor,
        IStringLocalizer<BingMultiTenancyResource> stringLocalizer)
    {
        TenantResolver = tenantResolver;
        TenantStore = tenantStore;
        TenantResolveResultAccessor = tenantResolveResultAccessor;
        StringLocalizer = stringLocalizer;
    }

    /// <summary>
    /// 租户解析器
    /// </summary>
    protected virtual ITenantResolver TenantResolver { get; }

    /// <summary>
    /// 租户存储器
    /// </summary>
    protected virtual ITenantStore TenantStore { get; }

    /// <summary>
    /// 租户解析结果访问器
    /// </summary>
    protected virtual ITenantResolveResultAccessor TenantResolveResultAccessor { get; }

    /// <summary>
    /// 国际化
    /// </summary>
    protected virtual IStringLocalizer<BingMultiTenancyResource> StringLocalizer { get; }

    /// <summary>
    /// 获取租户配置
    /// </summary>
    /// <param name="saveResolveResult">是否保存解析结果。默认：false</param>
    public virtual async Task<TenantConfiguration> GetAsync(bool saveResolveResult = false)
    {
        var resolveResult = await TenantResolver.ResolveTenantIdOrNameAsync();
        if (saveResolveResult)
            TenantResolveResultAccessor.Result = resolveResult;

        TenantConfiguration tenant = null;
        if (resolveResult.TenantIdOrName != null)
        {
            tenant = await FindTenantAsync(resolveResult.TenantIdOrName);
            if (tenant == null)
                throw new BusinessException(
                    code: "Bing.MultiTenancy:010001",
                    message: StringLocalizer["TenantNotFoundMessage"],
                    details: StringLocalizer["TenantNotFoundDetails", resolveResult.TenantIdOrName]);
            if (!tenant.IsActive)
                throw new BusinessException(
                    code: "Bing.MultiTenancy:010002",
                    message: StringLocalizer["TenantNotActiveMessage"],
                    details: StringLocalizer["TenantNotActiveDetails", resolveResult.TenantIdOrName]);
        }

        return tenant;
    }

    /// <summary>
    /// 查找租户
    /// </summary>
    /// <param name="tenantIdOrName">租户标识、租户名称</param>
    protected virtual async Task<TenantConfiguration> FindTenantAsync(string tenantIdOrName)
    {
        if (Guid.TryParse(tenantIdOrName, out var parsedTenantId))
            return await TenantStore.FindByIdAsync(parsedTenantId.ToString());
        return await TenantStore.FindByNameAsync(tenantIdOrName);
    }
}

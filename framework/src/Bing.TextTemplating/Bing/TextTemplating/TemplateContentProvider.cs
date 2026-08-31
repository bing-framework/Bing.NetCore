using System.Globalization;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.TextTemplating;

/// <summary>
/// 使用已注册内容贡献者获取模板内容的默认提供程序。
/// </summary>
public class TemplateContentProvider : ITemplateContentProvider, ITransientDependency
{
    /// <summary>
    /// 获取用于解析内容贡献者的服务作用域工厂。
    /// </summary>
    public IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 获取文本模板选项配置。
    /// </summary>
    public BingTextTemplatingOptions Options { get; }

    /// <summary>
    /// 用于按名称获取模板定义的管理器。
    /// </summary>
    private readonly ITemplateDefinitionManager _templateDefinitionManager;

    /// <summary>
    /// 初始化一个<see cref="TemplateContentProvider"/>类型的实例
    /// </summary>
    /// <param name="templateDefinitionManager">模板定义管理器</param>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="options">文本模板选项配置</param>
    public TemplateContentProvider(
        ITemplateDefinitionManager templateDefinitionManager,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BingTextTemplatingOptions> options)
    {
        _templateDefinitionManager = templateDefinitionManager;
        ServiceScopeFactory = serviceScopeFactory;
        Options = options.Value;
    }

    /// <inheritdoc />
    public virtual Task<string> GetContentOrNullAsync(string templateName, string cultureName = null, bool tryDefaults = true,
        bool useCurrentCultureIfCultureNameIsNull = true)
    {
        var template = _templateDefinitionManager.Get(templateName);
        return GetContentOrNullAsync(template, cultureName);
    }

    /// <inheritdoc />
    /// <exception cref="Warning">未注册模板内容贡献者时抛出。</exception>
    /// <remarks>贡献者在独立 DI 作用域中按配置的逆序执行；该作用域会在异步操作完成后释放。</remarks>
    public virtual async Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true)
    {
        Check.NotNull(templateDefinition, nameof(templateDefinition));
        if (!Options.ContentContributors.Any())
            throw new Warning($"No template content contributor was registered. Use {nameof(BingTextTemplatingOptions)} to register contributors!");
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            string templateString = null;
            if (cultureName == null && useCurrentCultureIfCultureNameIsNull)
                cultureName = CultureInfo.CurrentUICulture.Name;
            var contributors = CreateTemplateContentContributors(scope.ServiceProvider);
            templateString = await GetContentOrNullAsync(contributors, new TemplateContentContributorContext(templateDefinition, scope.ServiceProvider, cultureName));
            if (templateString != null)
                return templateString;
            if (!tryDefaults)
                return null;
        }
        return null;
    }

    /// <summary>
    /// 从当前服务作用域创建已配置的模板内容贡献者。
    /// </summary>
    /// <param name="serviceProvider">用于解析贡献者的当前服务作用域服务提供程序。</param>
    /// <returns>按逆序配置排列的模板内容贡献者数组。</returns>
    /// <remarks>配置类型未注册或不兼容时，依赖注入解析异常会向调用方传播。</remarks>
    protected virtual ITemplateContentContributor[] CreateTemplateContentContributors(IServiceProvider serviceProvider)
    {
        return Options.ContentContributors
            .Select(type => (ITemplateContentContributor)serviceProvider.GetRequiredService(type))
            .Reverse()
            .ToArray();
    }

    /// <summary>
    /// 依次从模板内容贡献者获取模板内容。
    /// </summary>
    /// <param name="contributors">按优先级排列的模板内容贡献者数组。</param>
    /// <param name="context">传递给每个贡献者的模板内容上下文。</param>
    /// <returns>首个非 <c>null</c> 的模板内容；所有贡献者均未提供内容时返回 <c>null</c>。</returns>
    protected virtual async Task<string> GetContentOrNullAsync(ITemplateContentContributor[] contributors, TemplateContentContributorContext context)
    {
        foreach (var contributor in contributors)
        {
            var templateString = await contributor.GetOrNullAsync(context);
            if (templateString != null)
                return templateString;
        }
        return null;
    }
}

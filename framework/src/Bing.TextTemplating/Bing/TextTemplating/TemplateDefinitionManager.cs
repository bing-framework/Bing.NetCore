using System.Collections.Immutable;
using Bing.Collections;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.TextTemplating;

/// <summary>
/// 发现、缓存并查询文本模板定义的默认管理器。
/// </summary>
public class TemplateDefinitionManager : ITemplateDefinitionManager, ISingletonDependency
{
    /// <summary>
    /// 延迟创建并缓存的模板定义字典。
    /// </summary>
    protected Lazy<IDictionary<string, TemplateDefinition>> TemplateDefinitions { get; }

    /// <summary>
    /// 获取文本模板选项配置。
    /// </summary>
    protected BingTextTemplatingOptions Options { get; }

    /// <summary>
    /// 获取用于创建模板定义提供者作用域的根服务提供程序。
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 初始化一个<see cref="TemplateDefinitionManager"/>类型的实例
    /// </summary>
    /// <param name="options">文本模板选项配置</param>
    /// <param name="serviceProvider">服务提供程序</param>
    public TemplateDefinitionManager(
        IOptions<BingTextTemplatingOptions> options,
        IServiceProvider serviceProvider)
    {
        Options = options.Value;
        ServiceProvider = serviceProvider;
        TemplateDefinitions = new Lazy<IDictionary<string, TemplateDefinition>>();
    }

    /// <inheritdoc />
    public virtual TemplateDefinition Get(string name)
    {
        Check.NotNull(name, nameof(name));
        var template = GetOrNull(name);
        if (template == null)
            throw new Warning("Undefined template: " + name);
        return template;
    }

    /// <inheritdoc />
    public virtual IReadOnlyList<TemplateDefinition> GetAll() => TemplateDefinitions.Value.Values.ToImmutableList();

    /// <inheritdoc />
    public virtual TemplateDefinition GetOrNull(string name) => TemplateDefinitions.Value.GetOrDefault(name);

    /// <summary>
    /// 在临时 DI 作用域中创建模板定义字典。
    /// </summary>
    /// <returns>由已配置定义提供者构建的模板定义字典。</returns>
    /// <remarks>所有提供者先依次执行预定义阶段，再执行定义和后定义阶段；服务作用域随方法结束释放。</remarks>
    protected virtual IDictionary<string, TemplateDefinition> CreateTextTemplateDefinitions()
    {
        var templates = new Dictionary<string, TemplateDefinition>();
        using var scope = ServiceProvider.CreateScope();
        var providers = Options
            .DefinitionProviders
            .Select(x => scope.ServiceProvider.GetRequiredService(x) as ITemplateDefinitionProvider)
            .ToList();
        var context = new TemplateDefinitionContext(templates);
        foreach (var provider in providers)
            provider.PreDefine(context);
        foreach (var provider in providers)
            provider.Define(context);
        foreach (var provider in providers)
            provider.PostDefine(context);

        return templates;
    }
}
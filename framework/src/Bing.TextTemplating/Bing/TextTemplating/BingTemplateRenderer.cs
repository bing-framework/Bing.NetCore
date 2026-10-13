using Bing.Collections;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.TextTemplating;

/// <summary>
/// 根据模板定义选择渲染引擎并生成模板文本的默认实现。
/// </summary>
public class BingTemplateRenderer : ITemplateRenderer, ITransientDependency
{
    /// <summary>
    /// 获取用于解析渲染引擎的服务作用域工厂。
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 获取模板定义管理器。
    /// </summary>
    protected ITemplateDefinitionManager TemplateDefinitionManager { get; }

    /// <summary>
    /// 获取文本模板渲染选项。
    /// </summary>
    protected BingTextTemplatingOptions Options { get; }

    /// <summary>
    /// 初始化一个 <see cref="BingTemplateRenderer"/> 类型的实例。
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂。</param>
    /// <param name="templateDefinitionManager">模板定义管理器。</param>
    /// <param name="options">文本模板选项配置。</param>
    public BingTemplateRenderer(
        IServiceScopeFactory serviceScopeFactory,
        ITemplateDefinitionManager templateDefinitionManager,
        IOptions<BingTextTemplatingOptions> options)
    {
        ServiceScopeFactory = serviceScopeFactory;
        TemplateDefinitionManager = templateDefinitionManager;
        Options = options.Value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 根据模板定义选择渲染引擎并异步生成文本内容。
    /// </remarks>
    /// <exception cref="Warning">模板未找到可用的渲染引擎时抛出。</exception>
    public virtual async Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null)
    {
        var templateDefinition = TemplateDefinitionManager.Get(templateName);
        var renderEngine = templateDefinition.RenderEngine;
        if (renderEngine.IsNullOrWhiteSpace())
            renderEngine = Options.DefaultRenderingEngine;
        var providerType = Options.RenderingEngines.GetOrDefault(renderEngine);
        if (providerType != null && typeof(ITemplateRenderingEngine).IsAssignableFrom(providerType))
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var templateRenderingEngine = (ITemplateRenderingEngine)scope.ServiceProvider.GetRequiredService(providerType);
            return await templateRenderingEngine.RenderAsync(templateName, model, cultureName, globalContext);
        }
        throw new Warning("There is no rendering engine found with template name: " + templateName);
    }
}

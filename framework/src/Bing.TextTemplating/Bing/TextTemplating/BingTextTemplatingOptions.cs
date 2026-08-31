using Bing.Collections;

namespace Bing.TextTemplating;

/// <summary>
/// 配置文本模板定义、内容贡献者和渲染引擎注册表。
/// </summary>
public class BingTextTemplatingOptions
{
    /// <summary>
    /// 获取模板定义提供程序类型列表，默认初始化为空列表；注册项用于发现和构建模板定义。
    /// </summary>
    public ITypeList<ITemplateDefinitionProvider> DefinitionProviders { get; }

    /// <summary>
    /// 获取模板内容贡献者类型列表，默认初始化为空列表；注册项用于向模板渲染上下文贡献内容。
    /// </summary>
    public ITypeList<ITemplateContentContributor> ContentContributors { get; }

    /// <summary>
    /// 获取按引擎名称索引的渲染引擎实现类型字典，默认初始化为空字典；键名必须与模板选用的引擎名称一致。
    /// </summary>
    public IDictionary<string, Type> RenderingEngines { get; }

    /// <summary>
    /// 获取或设置默认渲染引擎名称。
    /// </summary>
    /// <remarks>模板未显式指定渲染引擎时使用此名称；该值可以为 <c>null</c>，非空时应与 <see cref="RenderingEngines"/> 中已注册的键一致。</remarks>
    public string DefaultRenderingEngine { get; set; }

    /// <summary>
    /// 初始化 <see cref="BingTextTemplatingOptions"/> 的实例及其空注册集合。
    /// </summary>
    public BingTextTemplatingOptions()
    {
        DefinitionProviders = new TypeList<ITemplateDefinitionProvider>();
        ContentContributors = new TypeList<ITemplateContentContributor>();
        RenderingEngines = new Dictionary<string, Type>();
    }
}

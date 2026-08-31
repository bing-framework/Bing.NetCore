namespace Bing.Core.Modularity;

/// <summary>
/// 表示模块在框架启动过程中的层级；数值越小越核心，通常越早启动。
/// </summary>
public enum ModuleLevel
{
    /// <summary>
    /// 核心模块级别；模块不依赖第三方组件，通常始终加载且不可替换。
    /// </summary>
    Core = 1,

    /// <summary>
    /// 框架模块级别；表示依赖第三方组件的基础设施模块。
    /// </summary>
    Framework = 10,

    /// <summary>
    /// 应用模块级别；表示面向应用数据和通用应用能力的基础模块。
    /// </summary>
    Application = 20,

    /// <summary>
    /// 业务模块级别；表示承载具体业务处理的模块。
    /// </summary>
    Business = 30
}

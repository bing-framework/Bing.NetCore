using System.ComponentModel;

namespace Bing.Core.Enums;

/// <summary>
/// 标识应用当前运行所处的环境阶段。
/// </summary>
public enum EnvironmentType : byte
{
    /// <summary>
    /// 面向开发和调试的环境。
    /// </summary>
    [Description("开发环境")]
    Development = 1,

    /// <summary>
    /// 面向功能或集成测试的环境。
    /// </summary>
    [Description("测试环境")]
    Test = 2,

    /// <summary>
    /// 面向发布前验证或预览的环境。
    /// </summary>
    [Description("预览环境")]
    Preview = 3,

    /// <summary>
    /// 面向最终用户提供正式服务的环境。
    /// </summary>
    [Description("生产环境")]
    Prod = 4
}

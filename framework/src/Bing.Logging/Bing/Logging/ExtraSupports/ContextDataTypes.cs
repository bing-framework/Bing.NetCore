namespace Bing.Logging.ExtraSupports;

/// <summary>
/// 日志事件上下文数据类型
/// </summary>
public class ContextDataTypes
{
    /// <summary>
    /// 日志上下文扩展属性键前缀，后接属性名称；值为 <c>__BING_EXTRA_PROPERTY_</c>。
    /// </summary>
    public const string ExtraProperty = "__BING_EXTRA_PROPERTY_";

    /// <summary>
    /// 日志上下文标签列表键，值为 <c>__BING_TAGS</c>。
    /// </summary>
    public const string Tags = "__BING_TAGS";

    /// <summary>
    /// 日志上下文调用者信息键，值为 <c>__BING_CALLER_IFNO</c>。
    /// </summary>
    public const string CallerInfo = "__BING_CALLER_IFNO";
}

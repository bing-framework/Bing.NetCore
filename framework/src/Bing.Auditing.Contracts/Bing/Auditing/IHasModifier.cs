namespace Bing.Auditing;

/// <summary>
/// 定义使用字符串标识表示最后修改人的审计契约。
/// </summary>
public interface IHasModifier : IHasModifier<string> { }

/// <summary>
/// 定义使用指定标识类型表示最后修改人的审计契约。
/// </summary>
public interface IHasModifier<TModifier>
{
    /// <summary>
    /// 获取或设置最后修改该实体的用户或主体标识。
    /// </summary>
    TModifier LastModifier { get; set; }
}
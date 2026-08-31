namespace Bing.Auditing;

/// <summary>
/// 定义使用字符串标识表示创建人的审计契约。
/// </summary>
public interface IHasCreator : IHasCreator<string> { }

/// <summary>
/// 定义使用指定标识类型表示创建人的审计契约。
/// </summary>
public interface IHasCreator<TCreator>
{
    /// <summary>
    /// 获取或设置创建该实体的用户或主体标识。
    /// </summary>
    TCreator Creator { get; set; }
}
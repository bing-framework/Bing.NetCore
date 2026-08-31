namespace Bing.Auditing;

/// <summary>
/// 定义使用字符串标识表示删除人的审计契约。
/// </summary>
public interface IHasDeleter : IHasDeleter<string> { }

/// <summary>
/// 定义使用指定标识类型表示删除人的审计契约。
/// </summary>
public interface IHasDeleter<TDeleter>
{
    /// <summary>
    /// 获取或设置删除该实体的用户或主体标识。
    /// </summary>
    TDeleter Deleter { get; set; }
}
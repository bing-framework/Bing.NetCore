namespace Bing.Auditing;

/// <summary>
/// 删除人
/// </summary>
public interface IHasDeleter : IHasDeleter<string> { }

/// <summary>
/// 删除人
/// </summary>
public interface IHasDeleter<TDeleter>
{
    /// <summary>
    /// 删除人
    /// </summary>
    TDeleter Deleter { get; set; }
}
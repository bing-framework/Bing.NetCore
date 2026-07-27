namespace Bing.Data.Queries;

/// <summary>
/// 控制查询结果实体跟踪行为的契约。
/// </summary>
public interface ITrack
{
    /// <summary>
    /// 将当前查询设置为不跟踪返回的实体。
    /// </summary>
    void NoTracking();
}
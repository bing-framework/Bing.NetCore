namespace Bing.Monitoring.Health;

/// <summary>
/// 定义业务健康检查
/// </summary>
public interface IBusHealth
{
    /// <summary>
    /// 获取业务名称
    /// </summary>
    string Name { get; }
}

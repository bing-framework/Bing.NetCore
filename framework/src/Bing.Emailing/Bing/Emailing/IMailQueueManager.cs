namespace Bing.Emailing;

/// <summary>
/// 邮件队列管理器
/// </summary>
public interface IMailQueueManager
{
    /// <summary>
    /// 获取是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 获取队列数
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 启动邮件队列的后台发送循环。
    /// </summary>
    void Run();

    /// <summary>
    /// 停止邮件队列的后台发送循环。
    /// </summary>
    void Stop();
}
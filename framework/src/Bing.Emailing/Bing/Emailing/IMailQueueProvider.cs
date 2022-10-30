namespace Bing.Emailing;

/// <summary>
/// 邮件队列提供程序
/// </summary>
public interface IMailQueueProvider
{
    /// <summary>
    /// 队列邮件数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 队列是否为空
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="box">电子邮件</param>
    void Enqueue(EmailBox box);

    /// <summary>
    /// 尝试出队，获取电子邮件
    /// </summary>
    /// <param name="box">电子邮件</param>
    bool TryDequeue(out EmailBox box);
}
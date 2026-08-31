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
    /// <returns>成功取出邮件时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool TryDequeue(out EmailBox box);
}
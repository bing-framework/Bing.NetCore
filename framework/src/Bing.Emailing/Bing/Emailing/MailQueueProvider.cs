using System.Collections.Concurrent;

namespace Bing.Emailing;

/// <summary>
/// 邮件队列提供程序
/// </summary>
public class MailQueueProvider : IMailQueueProvider
{
    /// <summary>
    /// 线程安全的邮件队列
    /// </summary>
    private static readonly ConcurrentQueue<EmailBox> MailQueue = new ConcurrentQueue<EmailBox>();

    /// <inheritdoc />
    public int Count => MailQueue.Count;

    /// <inheritdoc />
    public bool IsEmpty => MailQueue.IsEmpty;

    /// <inheritdoc />
    public void Enqueue(EmailBox box) => MailQueue.Enqueue(box);

    /// <inheritdoc />
    public bool TryDequeue(out EmailBox box) => MailQueue.TryDequeue(out box);
}
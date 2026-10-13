namespace Bing.EasyCaching;

/// <summary>
/// 为 Redis outbox 提供 dead-letter 管理操作。
/// </summary>
internal interface IHybridInvalidationDeadLetterAdminOutbox
{
    /// <summary>
    /// 读取一批 dead-letter 记录。
    /// </summary>
    /// <param name="maximumMessages">最多读取的消息数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>读取到的 dead-letter 记录。</returns>
    Task<IReadOnlyList<HybridInvalidationDeadLetterRecord>> ReadDeadLettersAsync(
        int maximumMessages, CancellationToken cancellationToken);

    /// <summary>
    /// 读取句柄对应的 dead-letter payload。
    /// </summary>
    /// <param name="handle">dead-letter 句柄。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>payload 内容；记录不存在时返回 <see langword="null"/>。</returns>
    Task<string> GetDeadLetterPayloadAsync(string handle, CancellationToken cancellationToken);

    /// <summary>
    /// 将 dead-letter 重新放回待投递队列。
    /// </summary>
    /// <param name="handle">dead-letter 句柄。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>记录存在并成功重放时返回 <see langword="true"/>。</returns>
    Task<bool> ReplayDeadLetterAsync(string handle, CancellationToken cancellationToken);

    /// <summary>
    /// 删除句柄对应的 dead-letter 记录。
    /// </summary>
    /// <param name="handle">dead-letter 句柄。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>记录存在并成功删除时返回 <see langword="true"/>。</returns>
    Task<bool> DeleteDeadLetterAsync(string handle, CancellationToken cancellationToken);
}

/// <summary>
/// 表示 Redis outbox 中的一条 dead-letter 记录。
/// </summary>
internal sealed class HybridInvalidationDeadLetterRecord
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDeadLetterRecord"/> 类型的实例。
    /// </summary>
    /// <param name="payload">记录的 JSON payload。</param>
    /// <param name="deadLetteredAtUtc">记录进入 dead-letter 的时间。</param>
    /// <param name="handle">可选的不透明句柄。</param>
    internal HybridInvalidationDeadLetterRecord(string payload,
        DateTimeOffset? deadLetteredAtUtc, string handle = null)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        DeadLetteredAtUtc = deadLetteredAtUtc;
        Handle = handle ?? HybridInvalidationDeadLetterHandle.Create(payload);
    }

    /// <summary>
    /// 获取记录的 JSON payload。
    /// </summary>
    internal string Payload { get; }

    /// <summary>
    /// 获取记录进入 dead-letter 的时间。
    /// </summary>
    internal DateTimeOffset? DeadLetteredAtUtc { get; }

    /// <summary>
    /// 获取记录的不透明句柄。
    /// </summary>
    internal string Handle { get; }
}

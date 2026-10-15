namespace Bing.Events.Ordering;

/// <summary>
/// 校验有序事件使用的逻辑消费者名称。
/// </summary>
internal static class OrderedEventConsumerName
{
    /// <summary>
    /// 校验消费者名称的长度和边界字符。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="parameterName">参数名称。</param>
    internal static void Validate(string consumer, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(consumer))
            throw new ArgumentException("逻辑消费者不能为空。", parameterName);
        if (consumer.Length > 200)
            throw new ArgumentException("逻辑消费者不能超过 200 个字符。", parameterName);
        if (!string.Equals(consumer, consumer.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("逻辑消费者不能包含首尾空白。", parameterName);
    }
}

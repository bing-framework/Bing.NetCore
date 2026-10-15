using System;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 统一校验审计日志查询条件。
/// </summary>
internal static class AuditLogQueryValidator
{
    /// <summary>
    /// 单次查询允许返回的最大记录数。
    /// </summary>
    internal const int MaxLimit = 1000;

    /// <summary>
    /// 单次查询允许跳过的最大记录数。
    /// </summary>
    internal const int MaxOffset = 1000000;

    /// <summary>
    /// 校验查询条件。
    /// </summary>
    /// <param name="query">查询条件。</param>
    internal static void Validate(AuditLogQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (query.TenantId == null)
            throw new ArgumentException("审计查询必须显式提供租户标识。", nameof(query));

        AuditLogTenantIdentity.Validate(query.TenantId, nameof(query.TenantId), false);
        if (query.Offset < 0 || query.Offset > MaxOffset)
            throw new ArgumentOutOfRangeException(nameof(query.Offset),
                $"审计查询偏移必须位于 0 到 {MaxOffset} 之间。");
        if (query.Limit <= 0 || query.Limit > MaxLimit)
            throw new ArgumentOutOfRangeException(nameof(query.Limit),
                $"审计查询批量必须位于 1 到 {MaxLimit} 之间。");
        if (query.FromUtc.HasValue && query.FromUtc.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计查询起始时间必须使用 UTC。", nameof(query));
        if (query.ToUtc.HasValue && query.ToUtc.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计查询结束时间必须使用 UTC。", nameof(query));
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value >= query.ToUtc.Value)
            throw new ArgumentException("审计查询时间范围必须满足起始时间早于结束时间。", nameof(query));

        ValidateText(query.ApplicationName, 256, nameof(query.ApplicationName));
        ValidateText(query.UserId, 128, nameof(query.UserId));
        ValidateText(query.CorrelationId, 128, nameof(query.CorrelationId));
    }

    /// <summary>
    /// 校验可选文本条件的长度。
    /// </summary>
    /// <param name="value">文本值。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="name">参数名称。</param>
    private static void ValidateText(string value, int maxLength, string name)
    {
        if (value != null && value.Length > maxLength)
            throw new ArgumentOutOfRangeException(name, $"审计查询条件不能超过 {maxLength} 个字符。");
    }
}

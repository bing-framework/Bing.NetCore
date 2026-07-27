namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 参数数量上限提供程序。
/// </summary>
public interface ISqlParameterLimitProvider
{
    /// <summary>
    /// 最大参数数量；未限制时返回 <see langword="null"/>。
    /// </summary>
    int? MaxParameterCount { get; }
}
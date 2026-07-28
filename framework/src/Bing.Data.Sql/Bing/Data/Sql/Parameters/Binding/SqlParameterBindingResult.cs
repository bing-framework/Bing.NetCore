namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定结果
/// </summary>
public sealed class SqlParameterBindingResult
{
    /// <summary>
    /// 参数绑定项
    /// </summary>
    public IReadOnlyList<SqlParameterBindingItem> Items { get; set; } = Array.Empty<SqlParameterBindingItem>();

    /// <summary>
    /// 原始参数类型名称
    /// </summary>
    public string OriginalParameterType { get; set; }

    /// <summary>
    /// 是否使用元数据绑定
    /// </summary>
    public bool IsMetadataBound { get; set; }
}
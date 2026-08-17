namespace Bing.Data.Sql;

/// <summary>
/// Builder 单次执行使用的冻结渲染快照。
/// </summary>
public sealed class SqlBuilderExecutionSnapshot
{
    /// <summary>
    /// 初始化 Builder 执行快照。
    /// </summary>
    /// <param name="sql">冻结后的 SQL 文本。</param>
    /// <param name="builder">与 SQL 使用同一参数状态的 Builder。</param>
    public SqlBuilderExecutionSnapshot(string sql, ISqlBuilder builder)
    {
        Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// 冻结后的 SQL 文本。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 与 SQL 使用同一参数状态的 Builder。
    /// </summary>
    public ISqlBuilder Builder { get; }
}
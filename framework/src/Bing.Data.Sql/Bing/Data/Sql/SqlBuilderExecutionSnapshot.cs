using Bing.Data.Sql.Builders.Params;

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
    /// <param name="parameters">与 SQL 使用同一参数状态的参数快照。</param>
    /// <param name="debugSql">与 SQL 使用同一参数状态生成的调试 SQL。</param>
    public SqlBuilderExecutionSnapshot(string sql, IEnumerable<SqlParam> parameters, string debugSql = null)
    {
        Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        Parameters = Array.AsReadOnly(parameters.Where(parameter => parameter != null)
            .Select(SqlParameterSnapshot.CloneSqlParameter)
            .ToArray());
        DebugSql = debugSql ?? sql;
    }

    /// <summary>
    /// 冻结后的 SQL 文本。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 与 SQL 使用同一参数状态生成的调试 SQL。
    /// </summary>
    public string DebugSql { get; }

    /// <summary>
    /// 与 SQL 使用同一参数状态的不可变参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters { get; }
}
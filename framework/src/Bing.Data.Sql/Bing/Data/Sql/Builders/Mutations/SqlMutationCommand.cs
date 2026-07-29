using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 可执行的实体写入 SQL 命令快照。
/// </summary>
public sealed class SqlMutationCommand
{
    /// <summary>
    /// 初始化一个<see cref="SqlMutationCommand"/>类型的实例。
    /// </summary>
    /// <param name="sql">已生成的 SQL 语句。</param>
    /// <param name="parameters">已生成的参数集合。</param>
    public SqlMutationCommand(string sql, IReadOnlyCollection<SqlParam> parameters)
    {
        Sql = string.IsNullOrWhiteSpace(sql) ? throw new ArgumentException("SQL 语句不能为空。", nameof(sql)) : sql;
        Parameters = parameters ?? Array.Empty<SqlParam>();
    }

    /// <summary>
    /// 已生成的 SQL 语句。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 已生成的参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters { get; }
}
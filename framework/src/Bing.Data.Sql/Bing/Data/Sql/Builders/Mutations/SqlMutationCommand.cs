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
    /// <param name="validateAffectedRows">是否在实际受影响行数不符合预期时抛出并发异常。</param>
    public SqlMutationCommand(string sql, IReadOnlyCollection<SqlParam> parameters, bool validateAffectedRows = false)
    {
        Sql = string.IsNullOrWhiteSpace(sql) ? throw new ArgumentException("SQL 语句不能为空。", nameof(sql)) : sql;
        Parameters = parameters ?? Array.Empty<SqlParam>();
        ValidateAffectedRows = validateAffectedRows;
    }

    /// <summary>
    /// 已生成的 SQL 语句。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 已生成的参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters { get; }

    /// <summary>
    /// 是否要求实际受影响行数为一行。
    /// </summary>
    public bool ValidateAffectedRows { get; }
}
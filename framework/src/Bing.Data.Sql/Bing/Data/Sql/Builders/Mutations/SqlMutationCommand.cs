using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 可执行的实体写入 SQL 命令快照。
/// </summary>
public sealed class SqlMutationCommand
{
    /// <summary>
    /// 内部持有的参数快照，避免调用方通过公开集合修改命令输入。
    /// </summary>
    private readonly SqlParam[] _parameters;

    /// <summary>
    /// 初始化一个<see cref="SqlMutationCommand"/>类型的实例。
    /// </summary>
    /// <param name="sql">已生成的 SQL 语句。</param>
    /// <param name="parameters">已生成的参数集合。</param>
    /// <param name="validateAffectedRows">是否在实际受影响行数不符合预期时抛出并发异常。</param>
    public SqlMutationCommand(string sql, IReadOnlyCollection<SqlParam> parameters, bool validateAffectedRows = false)
    {
        Sql = string.IsNullOrWhiteSpace(sql) ? throw new ArgumentException("SQL 语句不能为空。", nameof(sql)) : sql;
        _parameters = parameters?.Where(parameter => parameter != null)
            .Select(CloneParameter)
            .ToArray() ?? Array.Empty<SqlParam>();
        ValidateAffectedRows = validateAffectedRows;
    }

    /// <summary>
    /// 已生成的 SQL 语句。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 已生成的参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters => _parameters.Select(CloneParameter).ToArray();

    /// <summary>
    /// 是否要求实际受影响行数为一行。
    /// </summary>
    public bool ValidateAffectedRows { get; }

    /// <summary>
    /// 创建包含独立值容器和元数据的增强参数副本。
    /// </summary>
    /// <param name="parameter">待复制的增强参数。</param>
    /// <returns>独立的增强参数副本。</returns>
    private static SqlParam CloneParameter(SqlParam parameter) => SqlMutationParameter.Create(parameter).CreateSqlParam();
}
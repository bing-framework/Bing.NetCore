using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Multiple;

/// <summary>
/// 一次数据库往返执行的多结果集 SQL 命令快照。
/// </summary>
public sealed class SqlMultipleQueryCommand
{
    /// <summary>
    /// 命令内部参数快照。
    /// </summary>
    private readonly IReadOnlyCollection<SqlParam> _parameters;

    /// <summary>
    /// 初始化一个<see cref="SqlMultipleQueryCommand"/>类型的实例。
    /// </summary>
    /// <param name="sql">组合后的 SQL 语句。</param>
    /// <param name="parameters">组合后的参数快照。</param>
    public SqlMultipleQueryCommand(string sql, IReadOnlyCollection<SqlParam> parameters)
    {
        Sql = string.IsNullOrWhiteSpace(sql) ? throw new ArgumentException("多结果集 SQL 语句不能为空。", nameof(sql)) : sql;
        _parameters = Array.AsReadOnly((parameters ?? Array.Empty<SqlParam>()).Where(parameter => parameter != null)
            .Select(CloneParameter).ToArray());
    }

    /// <summary>
    /// 组合后的 SQL 语句。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 组合后的参数快照副本。
    /// </summary>
    /// <remarks>
    /// 每次访问均返回独立的参数副本，调用方修改返回项不会影响命令内部快照。
    /// </remarks>
    public IReadOnlyCollection<SqlParam> Parameters =>
        Array.AsReadOnly(_parameters.Select(CloneParameter).ToArray());

    /// <summary>
    /// 克隆参数元数据，避免调用方修改源参数影响已生成的命令。
    /// </summary>
    /// <param name="parameter">源参数。</param>
    /// <returns>独立的参数快照。</returns>
    private static SqlParam CloneParameter(SqlParam parameter)
    {
        return new SqlParam(parameter.Name, CloneValue(parameter.Value), parameter.DbType, parameter.Direction, parameter.Size,
            parameter.Precision, parameter.Scale)
        {
            OriginalValue = CloneValue(parameter.OriginalValue),
            EntityType = parameter.EntityType,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            DatabaseType = parameter.DatabaseType,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel,
            StorageKind = parameter.StorageKind,
            ConverterKind = parameter.ConverterKind,
            CustomConverterName = parameter.CustomConverterName
        };
    }

    /// <summary>
    /// 复制数组值，防止可变元素容器泄漏到命令快照外部。
    /// </summary>
    private static object CloneValue(object value) => value is Array array ? array.Clone() : value;
}
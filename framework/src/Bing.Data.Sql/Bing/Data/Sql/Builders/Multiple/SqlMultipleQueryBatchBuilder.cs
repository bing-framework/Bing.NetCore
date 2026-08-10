using System.Text;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Multiple;

/// <summary>
/// 默认多结果集批处理命令 Builder。
/// </summary>
public sealed class SqlMultipleQueryBatchBuilder : ISqlMultipleQueryBatchBuilder
{
    /// <summary>
    /// 批处理语句分隔符。
    /// </summary>
    private readonly char _separator;

    /// <summary>
    /// 已追加的 SQL 语句。
    /// </summary>
    private readonly List<string> _statements = new();

    /// <summary>
    /// 已追加的参数快照。
    /// </summary>
    private readonly List<SqlParam> _parameters = new();

    /// <summary>
    /// 已使用的参数名。
    /// </summary>
    private readonly HashSet<string> _parameterNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 初始化一个<see cref="SqlMultipleQueryBatchBuilder"/>类型的实例。
    /// </summary>
    /// <param name="separator">批处理语句分隔符。</param>
    public SqlMultipleQueryBatchBuilder(char separator)
    {
        _separator = separator;
    }

    /// <inheritdoc />
    public ISqlMultipleQueryBatchBuilder Append(string sql, IEnumerable<SqlParam> parameters = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("批处理 SQL 语句不能为空。", nameof(sql));
        var statement = sql.Trim().TrimEnd(_separator).Trim();
        if (statement.Length == 0)
            throw new ArgumentException("批处理 SQL 语句不能为空。", nameof(sql));
        var items = parameters?.Where(parameter => parameter != null).ToList() ?? new List<SqlParam>();
        var parameterNames = new HashSet<string>(_parameterNames, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in items)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                throw new ArgumentException("批处理参数名称不能为空。", nameof(parameters));
            if (parameterNames.Add(parameter.Name) == false)
                throw new InvalidOperationException($"批处理包含重复参数名称 {parameter.Name}。");
        }
        _statements.Add(statement);
        _parameters.AddRange(items.Select(CloneParameter));
        _parameterNames.UnionWith(items.Select(parameter => parameter.Name));
        return this;
    }

    /// <inheritdoc />
    public SqlMultipleQueryCommand Build()
    {
        if (_statements.Count == 0)
            throw new InvalidOperationException("多结果集批处理至少需要一条 SQL 语句。");
        var sql = new StringBuilder();
        for (var index = 0; index < _statements.Count; index++)
        {
            if (index > 0)
                sql.Append(_separator).AppendLine();
            sql.Append(_statements[index]);
        }
        return new SqlMultipleQueryCommand(sql.ToString(), _parameters);
    }

    /// <summary>
    /// 创建参数快照，避免追加后调用方修改参数影响批处理命令。
    /// </summary>
    private static SqlParam CloneParameter(SqlParam parameter)
    {
        return new SqlParam(parameter.Name, CloneValue(parameter.Value), parameter.DbType, parameter.Direction,
            parameter.Size, parameter.Precision, parameter.Scale)
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
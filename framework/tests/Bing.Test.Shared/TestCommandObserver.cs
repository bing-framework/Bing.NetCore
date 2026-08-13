using Bing.Data.Sql.Builders.Params;

namespace Bing.Test.Shared;

/// <summary>
/// 用于测试的单次命令观察器。
/// </summary>
public sealed class TestCommandObserver
{
    /// <summary>
    /// 最近一次命令 SQL。
    /// </summary>
    public string CommandText { get; private set; }

    /// <summary>
    /// 最近一次命令的独立参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters { get; private set; } = Array.Empty<SqlParam>();

    /// <summary>
    /// 记录命令 SQL 和参数快照。
    /// </summary>
    /// <param name="commandText">命令 SQL。</param>
    /// <param name="parameters">命令参数。</param>
    public void Record(string commandText, IEnumerable<SqlParam> parameters)
    {
        CommandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
        Parameters = parameters?.Select(Clone).ToArray() ?? Array.Empty<SqlParam>();
    }

    /// <summary>
    /// 复制参数，避免测试观察结果随调用方容器变化。
    /// </summary>
    private static SqlParam Clone(SqlParam source) => new(source.Name, source.Value, source.DbType, source.Direction,
        source.Size, source.Precision, source.Scale)
    {
        OriginalValue = source.OriginalValue,
        EntityType = source.EntityType,
        PropertyName = source.PropertyName,
        ColumnName = source.ColumnName,
        DatabaseType = source.DatabaseType,
        ProviderTypeName = source.ProviderTypeName,
        Source = source.Source,
        MetadataLevel = source.MetadataLevel,
        StorageKind = source.StorageKind,
        ConverterKind = source.ConverterKind,
        CustomConverterName = source.CustomConverterName
    };
}
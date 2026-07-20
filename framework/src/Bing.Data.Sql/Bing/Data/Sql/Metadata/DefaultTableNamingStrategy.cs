namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认表命名策略
/// </summary>
public sealed class DefaultTableNamingStrategy : ITableNamingStrategy
{
    /// <inheritdoc />
    public string Resolve(string tableName, string logicalSchema, LogicalTableNamingMode namingMode)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));
        if (namingMode != LogicalTableNamingMode.Prefix || string.IsNullOrWhiteSpace(logicalSchema))
            return tableName;
        var prefix = $"{logicalSchema}_";
        return tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? tableName : $"{prefix}{tableName}";
    }
}
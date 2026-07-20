namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 表命名策略
/// </summary>
public interface ITableNamingStrategy
{
    /// <summary>
    /// 解析物理表名
    /// </summary>
    /// <param name="tableName">原始表名</param>
    /// <param name="logicalSchema">逻辑架构</param>
    /// <param name="namingMode">命名方式</param>
    string Resolve(string tableName, string logicalSchema, LogicalTableNamingMode namingMode);
}
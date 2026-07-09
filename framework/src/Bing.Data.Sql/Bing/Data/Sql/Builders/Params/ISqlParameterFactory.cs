using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// Sql 参数工厂
/// </summary>
public interface ISqlParameterFactory
{
    /// <summary>
    /// 创建 Sql 参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="source">参数来源</param>
    /// <returns>Sql 参数</returns>
    SqlParam Create(string name, object value, ColumnMappingMetadata column, DatabaseContext databaseContext,
        Type entityType = null, SqlParameterSource source = SqlParameterSource.Unknown);
}

using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 表引用验证器。
/// </summary>
public interface ISqlTableReferenceValidator
{
    /// <summary>
    /// 验证表引用是否合法。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    /// <param name="databaseType">数据库类型。</param>
    void Validate(SqlTableReference table, DatabaseType databaseType);
}
namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 表引用解析器。
/// </summary>
public interface ISqlTableReferenceResolver
{
    /// <summary>
    /// 解析实体对应的结构化 SQL 表引用。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="databaseContext">执行数据库上下文。</param>
    /// <returns>包含最终物理表名和数据源语义的表引用。</returns>
    SqlTableReference Resolve(Type entityType, DatabaseContext databaseContext);

    /// <summary>
    /// 解析显式表名对应的结构化 SQL 表引用。
    /// </summary>
    /// <param name="tableName">调用方提供的基础表名。</param>
    /// <param name="databaseContext">执行数据库上下文。</param>
    /// <returns>仅包含显式表名与执行上下文的表引用。</returns>
    SqlTableReference Resolve(string tableName, DatabaseContext databaseContext);
}
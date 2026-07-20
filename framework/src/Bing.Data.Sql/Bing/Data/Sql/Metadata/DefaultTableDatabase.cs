namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 表数据库
/// </summary>
public class DefaultTableDatabase : ITableDatabase
{
    /// <summary>
    /// SQL 表引用解析器。
    /// </summary>
    private readonly ISqlTableReferenceResolver _tableReferenceResolver;

    /// <summary>
    /// 初始化一个<see cref="DefaultTableDatabase"/>类型的实例。
    /// </summary>
    /// <param name="tableReferenceResolver">SQL 表引用解析器。</param>
    public DefaultTableDatabase(ISqlTableReferenceResolver tableReferenceResolver = null) =>
        _tableReferenceResolver = tableReferenceResolver;

    /// <summary>
    /// 获取数据库
    /// </summary>
    /// <param name="table">表</param>
    public string GetDatabase(string table) => _tableReferenceResolver?.Resolve(table, null).Catalog;
}

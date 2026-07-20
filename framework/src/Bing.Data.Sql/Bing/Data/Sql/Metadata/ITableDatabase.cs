namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 表数据库兼容接口。
/// </summary>
[Obsolete("请使用 ISqlTableReferenceResolver 解析结构化表引用。")]
public interface ITableDatabase
{
    /// <summary>
    /// 获取数据库
    /// </summary>
    /// <param name="table">表</param>
    string GetDatabase(string table);
}

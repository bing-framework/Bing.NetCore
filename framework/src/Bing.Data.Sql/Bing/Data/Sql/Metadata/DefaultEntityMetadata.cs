namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体元数据
/// </summary>
public class DefaultEntityMetadata : IEntityMetadata
{
    /// <summary>
    /// 获取表名
    /// </summary>
    /// <param name="type">实体类型</param>
    public string GetTable(Type type) => type?.Name;

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="type">实体类型</param>
    public string GetSchema(Type type) => string.Empty;

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="type">实体类型</param>
    /// <param name="property">属性名</param>
    public string GetColumn(Type type, string property) => property;
}

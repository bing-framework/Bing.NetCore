using Bing.Aspects;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体元数据。
/// </summary>
/// <remarks>
/// 此接口已过时，请改用 <see cref="IEntityModelMetadataProvider"/> 提供 ORM 原始映射信息。
/// </remarks>
[IgnoreAspect]
[Obsolete("请使用 IEntityModelMetadataProvider 提供实体模型原始元数据。")]
public interface IEntityMetadata
{
    /// <summary>
    /// 获取表名
    /// </summary>
    /// <param name="type">实体类型</param>
    string GetTable(Type type);

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="type">实体类型</param>
    string GetSchema(Type type);

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="type">实体类型</param>
    /// <param name="property">属性名</param>
    string GetColumn(Type type, string property);
}

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 解析实体的映射描述和元数据。
/// </summary>
public interface IEntityMappingResolver
{
    /// <summary>
    /// 获取实体描述信息。
    /// </summary>
    /// <param name="entityType">要获取描述信息的实体类型。</param>
    /// <returns>实体描述信息。</returns>
    EntityDescriptor GetDescriptor(Type entityType);

    /// <summary>
    /// 解析实体映射元数据。
    /// </summary>
    /// <param name="entityType">要解析映射元数据的实体类型。</param>
    /// <param name="databaseContext">用于确定数据库映射上下文的数据库上下文。</param>
    /// <returns>实体映射元数据。</returns>
    EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext);
}

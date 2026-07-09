namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射解析器
/// </summary>
public interface IEntityMappingResolver
{
    /// <summary>
    /// 获取实体描述信息
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体描述信息</returns>
    EntityDescriptor GetDescriptor(Type entityType);

    /// <summary>
    /// 解析实体映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>实体映射元数据</returns>
    EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext);
}

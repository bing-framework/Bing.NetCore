using Bing.Aspects;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体模型元数据提供器。
/// </summary>
/// <remarks>
/// 此接口仅暴露 ORM 或模型声明中的原始映射事实，不处理数据源、映射配置、表路由或 SQL 方言格式化。
/// </remarks>
[IgnoreAspect]
public interface IEntityModelMetadataProvider
{
    /// <summary>
    /// 获取实体模型元数据。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体模型元数据；未处理时返回 <see langword="null"/>。</returns>
    EntityModelMetadata GetMetadata(Type entityType);

    /// <summary>
    /// 获取实体模型元数据。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <returns>实体模型元数据；未处理时返回 <see langword="null"/>。</returns>
    EntityModelMetadata GetMetadata<TEntity>();
}
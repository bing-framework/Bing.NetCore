using Bing.Aspects;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体模型元数据提供器。
/// </summary>
/// <remarks>
/// 此接口仅暴露 ORM 或模型声明中的原始映射事实，不处理数据源、映射配置、表路由、逻辑命名或 SQL 方言格式化。
/// </remarks>
[IgnoreAspect]
public interface IEntityModelMetadataProvider
{
    /// <summary>
    /// 获取实体的基础表名。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>模型声明的基础表名；未声明时返回 <see langword="null"/>。</returns>
    string GetTableName(Type entityType);

    /// <summary>
    /// 获取实体的物理架构名称。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>模型声明的物理架构；未声明时返回 <see langword="null"/>。</returns>
    string GetPhysicalSchema(Type entityType);

    /// <summary>
    /// 获取实体的逻辑架构名称。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>模型声明的逻辑架构；未声明时返回 <see langword="null"/>。</returns>
    string GetLogicalSchema(Type entityType);

    /// <summary>
    /// 获取实体属性对应的原始列名。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>模型声明的列名；未声明时返回 <see langword="null"/>。</returns>
    string GetColumnName(Type entityType, string propertyName);
}
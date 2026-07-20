using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 查询元数据绑定器。
/// </summary>
internal interface ISqlQueryMetadataBinder
{
    /// <summary>
    /// 绑定实体元数据及其映射解析器。
    /// </summary>
    /// <param name="metadata">实体元数据。</param>
    /// <param name="resolver">实体映射解析器。</param>
    void BindEntityMetadata(IEntityMetadata metadata, IEntityMappingResolver resolver);
}

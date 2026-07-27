using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 查询元数据绑定器。
/// </summary>
public interface ISqlQueryMetadataBinder
{
    /// <summary>
    /// 绑定实体映射解析器。
    /// </summary>
    /// <param name="resolver">实体映射解析器。</param>
    void BindEntityMappingResolver(IEntityMappingResolver resolver);
}

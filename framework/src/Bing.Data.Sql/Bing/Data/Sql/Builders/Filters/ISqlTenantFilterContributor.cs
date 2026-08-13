namespace Bing.Data.Sql.Builders.Filters;

/// <summary>
/// 为 SQL 租户过滤提供实体适用范围和当前租户值。
/// </summary>
/// <remarks>
/// 实现由应用层适配当前租户上下文；SQL 核心不依赖具体的多租户组件或身份模型。
/// </remarks>
public interface ISqlTenantFilterContributor
{
    /// <summary>
    /// 判断实体是否需要租户边界。
    /// </summary>
    /// <param name="entityType">结构化表对应的实体类型。</param>
    /// <returns>需要追加租户谓词时返回 <c>true</c>。</returns>
    bool IsTenantEntity(Type entityType);

    /// <summary>
    /// 获取当前执行流的租户值。
    /// </summary>
    /// <param name="context">包含实体类型和数据库上下文的租户过滤上下文。</param>
    /// <returns>当前租户值；适用实体返回 <c>null</c> 或空白字符串时会拒绝渲染或执行。</returns>
    object GetTenantId(SqlTenantFilterContext context);
}

/// <summary>
/// 租户过滤取值上下文。
/// </summary>
public sealed class SqlTenantFilterContext
{
    /// <summary>
    /// 初始化一个 <see cref="SqlTenantFilterContext"/> 类型的实例。
    /// </summary>
    /// <param name="entityType">当前结构化表对应的实体类型。</param>
    /// <param name="databaseContext">当前 Builder 生命周期内固定的数据库上下文。</param>
    public SqlTenantFilterContext(Type entityType, DatabaseContext databaseContext)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        DatabaseContext = DatabaseContextSnapshot.Create(databaseContext);
    }

    /// <summary>
    /// 当前结构化表对应的实体类型。
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 当前 Builder 生命周期内固定的数据库上下文。
    /// </summary>
    public DatabaseContext DatabaseContext { get; }
}
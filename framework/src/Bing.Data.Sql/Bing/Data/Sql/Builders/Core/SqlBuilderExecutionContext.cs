using Bing.Data.Enums;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 生成器生命周期内固定的执行上下文。
/// </summary>
/// <remarks>
/// 该上下文只保存路由和数据源描述快照，不持有数据库连接、事务或其它执行资源。
/// </remarks>
internal sealed record SqlBuilderExecutionContext
{
    /// <summary>
    /// 固定的数据库上下文快照。
    /// </summary>
    public DatabaseContext DatabaseContext { get; }

    /// <summary>
    /// 数据源标识。
    /// </summary>
    public string DbKey => DatabaseContext?.DbKey;

    /// <summary>
    /// 固定的数据库类型。
    /// </summary>
    public DatabaseType? DatabaseType => DatabaseContext?.DataSource?.DatabaseType;

    /// <summary>
    /// 映射配置名称。
    /// </summary>
    public string MappingProfile => DatabaseContext?.MappingProfile;

    /// <summary>
    /// 租户标识。
    /// </summary>
    public string TenantId => DatabaseContext?.TenantId;

    /// <summary>
    /// 初始化一个<see cref="SqlBuilderExecutionContext"/>类型的实例。
    /// </summary>
    /// <param name="databaseContext">待固定的数据库上下文。</param>
    public SqlBuilderExecutionContext(DatabaseContext databaseContext)
    {
        DatabaseContext = DatabaseContextSnapshot.Create(databaseContext);
    }
}
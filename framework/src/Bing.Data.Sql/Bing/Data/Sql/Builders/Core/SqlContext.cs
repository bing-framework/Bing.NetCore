using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql执行上下文
/// </summary>
public class SqlContext
{
    /// <summary>
    /// Sql方言
    /// </summary>
    public IDialect Dialect { get; }

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    public IEntityAliasRegister EntityAliasRegister { get; }

    /// <summary>
    /// 实体元数据解析器
    /// </summary>
    public IEntityMetadata Metadata { get; }

    /// <summary>
    /// 参数管理器
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// Sql子句访问器
    /// </summary>
    public ISqlPartAccessor ClauseAccessor { get; }

    /// <summary>
    /// 初始化一个<see cref="SqlContext"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="entityAliasRegister">实体别名注册器</param>
    /// <param name="metadata">实体原始数据解析器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="clause">Sql子句访问器</param>
    public SqlContext(IDialect dialect, IEntityAliasRegister entityAliasRegister, IEntityMetadata metadata, IParameterManager parameterManager, ISqlPartAccessor clause)
    {
        EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
        Metadata = metadata ?? new DefaultEntityMetadata();
        Dialect = dialect;
        ParameterManager = parameterManager;
        ClauseAccessor = clause ?? throw new ArgumentNullException(nameof(clause));
    }
}

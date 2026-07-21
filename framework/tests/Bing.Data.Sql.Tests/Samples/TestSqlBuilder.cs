using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试Sql生成器
/// </summary>
public class TestSqlBuilder : SqlBuilderBase
{
    /// <inheritdoc />
    protected override DatabaseType ProviderDatabaseType => DatabaseType.SqlServer;

    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 初始化Sql生成器
    /// </summary>
    /// <param name="dialect">Sql 方言</param>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器</param>
    public TestSqlBuilder(IDialect dialect = null, IEntityModelMetadataProvider entityModelMetadataProvider = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        ISqlTableReferenceValidator tableReferenceValidator = null)
        : base(parameterManager, entityMappingResolver, databaseContextAccessor, sqlParameterFactory, metadataOptions,
            options, databaseContextResolver, objectNameFormatter, crossDatabaseQueryValidator,
            tableReferenceValidator, entityModelMetadataProvider)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    protected override IDialect GetDialect()
    {
        if (_dialect != null)
            return _dialect;
        return TestDialect.Instance;
    }

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var result = new TestSqlBuilder();
        result.Clone(this);
        return result;
    }

    /// <inheritdoc />
    public override ISqlBuilder New()
    {
        return new TestSqlBuilder(Dialect, parameterManager: ParameterManager,
            entityMappingResolver: EntityMappingResolver, databaseContextAccessor: DatabaseContextAccessor,
            sqlParameterFactory: SqlParameterFactory, metadataOptions: MetadataOptions, options: Options,
            databaseContextResolver: DatabaseContextResolver, objectNameFormatter: ObjectNameFormatter,
            crossDatabaseQueryValidator: CrossDatabaseQueryValidator, tableReferenceValidator: TableReferenceValidator);
    }

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";
}

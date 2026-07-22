using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// MySql 表连接子句
/// </summary>
public class MySqlJoinClause : JoinClause
{
    /// <summary>
    /// 是否拆分字符串表名中的句点。
    /// </summary>
    private readonly bool _splitStringTableName;

    /// <summary>
    /// 初始化一个<see cref="MySqlJoinClause"/>类型的实例
    /// </summary>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="dialect">方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
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
    /// <param name="splitStringTableName">是否拆分字符串表名中的句点</param>
    /// <param name="joinItems">已克隆的连接项。</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文。</param>
    public MySqlJoinClause(ISqlBuilder sqlBuilder, IDialect dialect, IEntityResolver resolver,
        IEntityAliasRegister register, IParameterManager parameterManager,
        IEntityMappingResolver entityMappingResolver = null, IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlParameterFactory sqlParameterFactory = null, SqlMetadataOptions metadataOptions = null,
        SqlOptions options = null, ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        ISqlTableReferenceValidator tableReferenceValidator = null,
        bool splitStringTableName = false,
        List<JoinItem> joinItems = null,
        DatabaseContext databaseContext = null)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, joinItems, entityMappingResolver,
            databaseContextAccessor, sqlParameterFactory, metadataOptions, options, databaseContextResolver,
            objectNameFormatter, crossDatabaseQueryValidator, tableReferenceValidator, databaseContext)
    {
        _splitStringTableName = splitStringTableName;
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, false, _splitStringTableName, type);

    /// <summary>
    /// 解析实际 MySQL 的反引号字符串表名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected override (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        if (_splitStringTableName)
            return base.ParseTableName(table, alias);
        var parsedTable = MySqlTableNameParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <inheritdoc />
    public override IJoinClause Clone(ISqlBuilder sqlBuilder, IEntityAliasRegister register,
        IParameterManager parameterManager) => new MySqlJoinClause(sqlBuilder, _dialect, _resolver, register,
        parameterManager, _entityMappingResolver, _databaseContextAccessor, _sqlParameterFactory, _metadataOptions,
        _sqlOptions, _databaseContextResolver, _objectNameFormatter, _crossDatabaseQueryValidator,
        _tableReferenceValidator, _splitStringTableName, CloneItems(register, parameterManager), _databaseContext);
}

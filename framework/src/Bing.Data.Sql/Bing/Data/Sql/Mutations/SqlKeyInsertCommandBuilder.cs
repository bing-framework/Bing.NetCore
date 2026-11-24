using System.Data;
using System.Text;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 构建单行生成键写入命令的 SQL 组件。
/// </summary>
/// <remarks>
/// 复用映射、参数和 Returning 的单行生成键命令；普通 Insert API 保持不变。
/// </remarks>
public sealed class SqlKeyInsertCommandBuilder
{
    private const string OracleIdentityOutput = "bing_identity_key";

    /// <summary>
    /// 保存生成键插入命令使用的 SQL 构建器。
    /// </summary>
    private readonly ISqlInsertBuilder _builder;
    /// <summary>
    /// 保存生成键插入使用的实体键策略。
    /// </summary>
    private readonly EntityKeyPolicy _policy;
    /// <summary>
    /// 初始化一个 <see cref="SqlKeyInsertCommandBuilder" /> 类型的实例。
    /// </summary>
    /// <param name="builder">用于构造实体键插入语句的 SQL 构造器。</param>
    /// <param name="policy">用于生成实体键的策略。</param>
    public SqlKeyInsertCommandBuilder(ISqlInsertBuilder builder, EntityKeyPolicy policy)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }
    /// <summary>
    /// 根据实体和键生成 SQL 插入命令。
    /// </summary>
    /// <param name="entity">要插入的实体。</param>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <returns>包含 SQL、参数及执行信息的写入命令。</returns>
    /// <remarks>
    /// 键和映射必须符合配置的生成策略；数据库自增键支持 SQL Server、PostgreSQL、SQLite、MySQL 和官方 Oracle Provider。
    /// </remarks>
    public SqlWriteCommand Build<TEntity>(TEntity entity) where TEntity : class
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (_policy.EntityType != typeof(TEntity)) throw new ArgumentException("策略实体类型不匹配。", nameof(entity));
        var services = _builder.MutationContext.Services;
        var context = services.DatabaseContextResolver.Resolve(services.Options);
        var mapping = services.EntityMappingResolver.Resolve(typeof(TEntity), context);
        if (!mapping.Columns.TryGetValue(_policy.KeyPropertyName, out var key)) throw new InvalidOperationException("主键没有 SQL 列映射。");
        _policy.ValidateMapping(key.IsKey && mapping.Columns.Values.Count(x => x.IsKey) == 1 &&
            key.DatabaseGeneratedOption != System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed &&
            (_policy.Kind != EntityKeyGenerationKind.DatabaseIdentity ||
             key.DatabaseGeneratedOption == System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity), key.IsDatabaseGenerated);
        var identity = _policy.Kind == EntityKeyGenerationKind.DatabaseIdentity;
        var provider = _builder.MutationContext.Provider;
        var type = provider.DatabaseType;
        var oracleIdentity = identity && type == DatabaseType.Oracle && provider.Key == "bing.oracle";
        if (identity && !oracleIdentity && type != DatabaseType.SqlServer && type != DatabaseType.PgSql && type != DatabaseType.Sqlite && type != DatabaseType.MySql)
            throw new NotSupportedException("当前 Provider 尚无已验证的生成键返回适配。");
        var returning = identity && type != DatabaseType.MySql && !oracleIdentity;
        var generatedBuilder = _builder as SqlInsertBuilder;
        var useGeneratedSql = generatedBuilder?.CanUseGeneratedEntitySql == true &&
            SqlMutationPlan.SupportsGeneratedSql(provider, services);
        _builder.InsertClause.Into(mapping.Table);
        var planCache = SqlMutationPlanCaches.Get(services.EntityMappingResolver, services.MetadataOptions,
            services.EntityModelMetadataProvider);
        var plan = planCache.GetOrAdd(mapping, provider.Key, SqlMutationOperation.Insert, null, null);
        var generatedSql = useGeneratedSql
            ? identity ? plan.GetGeneratedKeyInsertSql(provider.Key) : plan.GetGeneratedSql(provider.Key)
            : null;
        var columns = plan.WriteColumns.Where(x => !x.IsDatabaseGenerated).ToArray();
        if (columns.Length == 0)
        {
            if (!identity) throw new InvalidOperationException("实体没有可插入列。");
            if (generatedSql != null)
            {
                // 常量路径跳过表名渲染，需要直接验证；回退路径由渲染器验证。
                services.TableReferenceValidator.Validate(mapping.Table, type);
                if (returning) _builder.Returning(key.ColumnName);
                var validation = new SqlValidationContext(provider, _builder.MutationContext.ParameterManager.Count,
                    false, SqlExecutionKind.Insert);
                _builder.InsertClause.Validate(validation);
                if (returning) generatedBuilder.ReturningClause.Validate(validation);
                generatedBuilder.ValidateParameterLimit(_builder.MutationContext.ParameterManager);
            }
            if (generatedSql == null)
            {
                var sql = new StringBuilder(); _builder.InsertClause.AppendTo(sql);
                if (type == DatabaseType.SqlServer) sql.Append(" OUTPUT INSERTED.").Append(provider.Dialect.SafeName(key.ColumnName));
                if (oracleIdentity)
                {
                    var keyColumn = provider.Dialect.SafeName(key.ColumnName);
                    sql.Append(" (").Append(keyColumn).Append(") Values (DEFAULT)")
                        .Append(OracleIdentitySuffix(keyColumn));
                }
                else sql.Append(type == DatabaseType.MySql ? " () VALUES ()" : " DEFAULT VALUES");
                if (type == DatabaseType.Sqlite || type == DatabaseType.PgSql) sql.Append(" RETURNING ").Append(provider.Dialect.SafeName(key.ColumnName));
                generatedSql = sql.ToString();
            }
            var emptyParameters = oracleIdentity ? new[] { CreateOracleIdentityOutput() } : Array.Empty<SqlParam>();
            var emptyCommand = new SqlWriteCommand(generatedSql, emptyParameters, provider,
                SqlOperationKind.InsertValues, returning);
            return plan.CanUseGeneratedParameterBinding
                ? emptyCommand.WithGeneratedParameterBinder(SqlGeneratedParameterBinding.Bind)
                : emptyCommand;
        }
        _builder.InsertColumnsClause.AddRange(columns.Select(x => x.ColumnName));
        var parameters = columns.Select(column => services.ParameterFactory.Create(
            _builder.MutationContext.ParameterManager.GenerateName(),
            plan.GetValue(entity, column, planCache), column, context, typeof(TEntity),
            SqlParameterSource.SqlBuilder)).ToArray();
        _builder.ValuesClause.AddRow(parameters);
        if (returning) _builder.Returning(key.ColumnName);
        // 空参数管理器也可能已推进名称序列；只有默认起点才能套用编译期占位符。
        if (generatedSql != null && parameters[0].Name != (type == DatabaseType.Oracle ? ":p_0" : "@_p_0"))
            generatedSql = null;
        if (generatedSql != null)
            services.TableReferenceValidator.Validate(mapping.Table, type);
        var command = generatedSql != null ? generatedBuilder.BuildCommand(generatedSql, returning) : _builder.ToSqlWriteCommand();
        if (oracleIdentity)
        {
            var sql = generatedSql ?? command.Sql + OracleIdentitySuffix(provider.Dialect.SafeName(key.ColumnName));
            command = new SqlWriteCommand(sql, command.CreateParameters().Append(CreateOracleIdentityOutput()),
                provider, SqlOperationKind.InsertValues);
        }
        return plan.CanUseGeneratedParameterBinding
            ? command.WithGeneratedParameterBinder(SqlGeneratedParameterBinding.Bind)
            : command;
    }

    private static string OracleIdentitySuffix(string keyColumn) => $" Returning {keyColumn} Into :{OracleIdentityOutput}";

    // 用 DbType 请求 CLR decimal，以完整保留 Oracle NUMBER 到 Int64 的精度。
    private static SqlParam CreateOracleIdentityOutput() =>
        new(OracleIdentityOutput, null, DbType.Decimal, ParameterDirection.Output);
}

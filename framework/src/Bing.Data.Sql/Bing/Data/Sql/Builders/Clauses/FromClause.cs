using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Properties;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// From子句
/// </summary>
public class FromClause : IFromClause
{
    /// <summary>
    /// Sql项
    /// </summary>
    protected SqlItem Table;

    /// <summary>
    /// Sql方言
    /// </summary>
    protected readonly IDialect Dialect;

    /// <summary>
    /// 实体解析器
    /// </summary>
    protected readonly IEntityResolver Resolver;

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    protected readonly IEntityAliasRegister Register;

    /// <summary>
    /// Sql生成器
    /// </summary>
    protected readonly ISqlBuilder Builder;

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    protected readonly ISqlObjectNameFormatter ObjectNameFormatter;

    /// <summary>
    /// 独立子句使用的固定数据库类型。
    /// </summary>
    protected readonly DatabaseType? ProviderDatabaseType;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected readonly ISqlTableReferenceValidator TableReferenceValidator;

    /// <summary>
    /// 初始化一个<see cref="FromClause"/>类型的实例
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="table">表</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="providerDatabaseType">独立子句使用的固定数据库类型</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器</param>
    public FromClause(ISqlBuilder builder
        , IDialect dialect
        , IEntityResolver resolver
        , IEntityAliasRegister register
        , SqlItem table = null
        , ISqlObjectNameFormatter objectNameFormatter = null
        , DatabaseType? providerDatabaseType = null
        , ISqlTableReferenceValidator tableReferenceValidator = null)
    {
        Builder = builder;
        Dialect = dialect;
        Resolver = resolver;
        Register = register;
        Table = table;
        ObjectNameFormatter = objectNameFormatter ?? new DefaultSqlObjectNameFormatter();
        ProviderDatabaseType = providerDatabaseType;
        TableReferenceValidator = tableReferenceValidator ?? new DefaultSqlTableReferenceValidator();
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    public virtual IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new FromClause(builder, Dialect, Resolver, register, Table, ObjectNameFormatter,
            ProviderDatabaseType, TableReferenceValidator);
    }

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void From(string table, string alias = null) => Table = CreateSqlItem(table, null, alias);

    /// <summary>
    /// 设置结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    public void From(SqlTableReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        Table = CreateStructuredSqlItem(reference);
        if (reference.EntityType == null)
            return;
        Register.Register(reference.EntityType, Resolver.GetAlias(reference.EntityType, reference.Alias));
        Register.FromType = reference.EntityType;
    }

    /// <summary>
    /// 创建Sql项
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    protected virtual SqlItem CreateSqlItem(string table, string schema, string alias) =>
        new SqlItem(table, schema, alias);

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public void From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        var type = typeof(TEntity);
        var reference = Resolver.GetTableReference(type).WithAlias(alias) with { EntityType = type };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference.WithPhysicalSchema(schema);
        From(reference);
    }

    /// <summary>
    /// 创建结构化表引用 Sql 项
    /// </summary>
    /// <param name="reference">结构化表引用</param>
    protected virtual SqlItem CreateStructuredSqlItem(SqlTableReference reference)
    {
        var sqlBuilder = Builder as SqlBuilderBase;
        var databaseContext = sqlBuilder?.GetDatabaseContext();
        var databaseType = sqlBuilder?.ResolveProviderDatabaseType(reference) ?? ProviderDatabaseType;
        return new StructuredSqlItem(reference, ObjectNameFormatter, databaseContext, databaseType,
            TableReferenceValidator);
    }

    /// <summary>
    /// 获取当前类型化 From 使用的结构化表引用。
    /// </summary>
    /// <returns>结构化表引用；原始字符串 From 返回 <see langword="null"/>。</returns>
    internal SqlTableReference GetStructuredReference() => (Table as StructuredSqlItem)?.Reference;

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public void From(ISqlBuilder builder, string alias)
    {
        if (builder == null)
            return;
        var result = builder.ToSql();
        if (string.IsNullOrWhiteSpace(alias) == false)
            result = $"({result}) As {Dialect.SafeName(alias)}";
        AppendSql(result);
    }

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public void From(Action<ISqlBuilder> action, string alias)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        From(builder, alias);
    }

    /// <summary>
    /// 添加到From子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, Dialect);
        if (Table != null && Table.Raw)
        {
            Table = new SqlItem($"{Table.Name}{sql}", raw: true);
            return;
        }
        Table = new SqlItem(sql, raw: true);
    }

    /// <summary>
    /// 验证
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Table?.Name))
            throw new InvalidOperationException(LibraryResource.TableIsEmpty);
    }

    /// <summary>
    /// 输出Sql
    /// </summary>
    public string ToSql()
    {
        var table = Table?.ToSql(Dialect);
        return string.IsNullOrWhiteSpace(table) ? null : $"From {table}";
    }
}
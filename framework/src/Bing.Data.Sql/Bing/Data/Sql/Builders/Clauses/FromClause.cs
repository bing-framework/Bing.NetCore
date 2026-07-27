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
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    protected IDialect Dialect => _context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    protected IEntityResolver Resolver => _context.EntityResolver;

    /// <summary>
    /// 实体别名注册器。
    /// </summary>
    protected IEntityAliasRegister Register => _context.AliasRegister;

    /// <summary>
    /// SQL 生成器。
    /// </summary>
    protected ISqlBuilder Builder => _context.Builder;

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    protected ISqlObjectNameFormatter ObjectNameFormatter => _context.Services.ObjectNameFormatter;

    /// <summary>
    /// 独立子句使用的固定数据库类型。
    /// </summary>
    protected readonly DatabaseType? ProviderDatabaseType;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected ISqlTableReferenceValidator TableReferenceValidator => _context.Services.TableReferenceValidator;

    /// <summary>
    /// SQL 字符串表引用解析器。
    /// </summary>
    protected ISqlTableReferenceParser TableReferenceParser => _context.Provider.TableReferenceParser;

    /// <summary>
    /// 初始化一个<see cref="FromClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public FromClause(SqlClauseContext context)
        : this(context, null, context?.Provider.DatabaseType)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="table">表。</param>
    /// <param name="providerDatabaseType">独立子句使用的固定数据库类型。</param>
    protected FromClause(SqlClauseContext context, SqlItem table, DatabaseType? providerDatabaseType)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Table = table;
        ProviderDatabaseType = providerDatabaseType;
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的 From 子句。</returns>
    public virtual IFromClause Clone(SqlClauseContext context)
    {
        if (context.AliasRegister != null)
            context.AliasRegister.FromType = Register.FromType;
        return CreateClone(context, Table?.Clone());
    }

    /// <summary>
    /// 创建克隆后的 From 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="table">已复制的表项。</param>
    /// <returns>保留 Provider 子类类型的 From 子句。</returns>
    protected virtual FromClause CreateClone(SqlClauseContext context, SqlItem table) =>
        new FromClause(context, table, ProviderDatabaseType);

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public void From(string table, string alias = null)
    {
        var parsedTable = ParseTableName(table, alias);
        Register?.RegisterAlias(parsedTable.Alias);
        Table = CreateSqlItem(parsedTable.TableName, parsedTable.Schema, parsedTable.Alias);
    }

    /// <summary>
    /// 解析字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected virtual (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        var parsedTable = TableReferenceParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <summary>
    /// 设置结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    public void From(SqlTableReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (reference.EntityType == null)
            Register?.RegisterAlias(reference.Alias);
        else
            Register?.Replace(reference.EntityType, Resolver.GetAlias(reference.EntityType, reference.Alias));
        Table = CreateStructuredSqlItem(reference);
        if (reference.EntityType == null)
            return;
        if (Register != null)
            Register.FromType = reference.EntityType;
    }

    /// <summary>
    /// 创建Sql项
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    protected virtual SqlItem CreateSqlItem(string table, string schema, string alias) =>
        SqlItem.Parse(table, schema, alias);

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public void From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        var type = typeof(TEntity);
        var reference = Resolver.GetTableReference(type) with { Alias = alias, EntityType = type };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
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
        var result = Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        Register?.RegisterAlias(alias);
        Table = SqlItem.Raw($"({result}) As {Dialect.SafeName(alias)}");
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
    /// 添加原始 From SQL。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        if (Table != null && Table.IsRaw)
        {
            Table = SqlItem.Raw($"{Table.Name}{sql}");
            return;
        }
        Table = SqlItem.Raw(sql);
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
using Bing.Data.Sql.Builders.Core;
using System.Text;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Properties;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// 默认 SQL Provider 的 From 子句实现。
/// </summary>
public class FromClause : IFromClause
{
    /// <summary>
    /// 当前查询源的 SQL 项，可能是字符串表、结构化表或子查询。
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
    /// 独立子句渲染结构化表引用时使用的固定数据库类型。
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
    /// 初始化绑定到指定运行上下文的 From 子句。
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void From(string table, string alias = null)
    {
        _context.UseOperation(SqlOperationAction.QueryClause);
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

    /// <inheritdoc />
    public void From(SqlTableReference reference)
    {
        _context.UseOperation(SqlOperationAction.QueryClause);
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
    /// 创建保存字符串表引用的 SQL 项。
    /// </summary>
    /// <param name="table">已解析的表名。</param>
    /// <param name="schema">已解析的架构名。</param>
    /// <param name="alias">已解析的表别名。</param>
    /// <returns>用于默认方言渲染的 SQL 表项。</returns>
    protected virtual SqlItem CreateSqlItem(string table, string schema, string alias) =>
        SqlItem.Parse(table, schema, alias);

    /// <inheritdoc />
    public void From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        var type = typeof(TEntity);
        var reference = Resolver.GetTableReference(type) with { Alias = alias, EntityType = type };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
        From(reference);
    }

    /// <summary>
    /// 创建用于延迟格式化和校验的结构化表引用 SQL 项。
    /// </summary>
    /// <param name="reference">待渲染的结构化表引用。</param>
    /// <returns>绑定当前 Builder 服务和数据库上下文的结构化 SQL 项。</returns>
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

    /// <inheritdoc />
    public void From(ISqlBuilder builder, string alias)
    {
        if (builder == null)
            return;
        var result = Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        Register?.RegisterAlias(alias);
        Table = SqlItem.Raw($"({result}) As {Dialect.SafeName(alias)}");
    }

    /// <inheritdoc />
    public void From(Action<ISqlBuilder> action, string alias)
    {
        if (action == null)
            return;
        var builder = Builder.New();
        action(builder);
        From(builder, alias);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Table?.Name))
            throw new InvalidOperationException(LibraryResource.TableIsEmpty);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var table = Table?.ToSql(Dialect);
        if (string.IsNullOrWhiteSpace(table))
            return;
        builder.Append("From ");
        builder.Append(table);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <summary>
    /// 输出Sql。
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.Length == 0 ? null : result.ToString();
    }
}
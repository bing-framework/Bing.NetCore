using Bing.Data.Sql.Builders.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 列
/// </summary>
public class ColumnItem
{
    /// <summary>
    /// 结构化聚合描述。
    /// </summary>
    private readonly SqlAggregateDescriptor _aggregate;

    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 表别名
    /// </summary>
    public string TableAlias { get; set; }

    /// <summary>
    /// 列别名
    /// </summary>
    public string ColumnAlias { get; set; }

    /// <summary>
    /// 是否使用原始值
    /// </summary>
    public bool Raw { get; }

    /// <summary>
    /// 表实体类型
    /// </summary>
    public Type TableType { get; }

    /// <summary>
    /// 结构化聚合函数。
    /// </summary>
    public SqlAggregateFunction? AggregateFunction => _aggregate?.Function;

    /// <summary>
    /// 是否对聚合参数去重。
    /// </summary>
    public bool AggregateDistinct => _aggregate?.Distinct == true;

    /// <summary>
    /// 是否使用聚合通配符参数。
    /// </summary>
    public bool AggregateWildcard => _aggregate?.ArgumentKind == SqlAggregateArgumentKind.Wildcard;

    /// <summary>
    /// 是否将聚合参数作为已解析的 SQL 片段。
    /// </summary>
    public bool AggregateArgumentRaw => _aggregate?.ArgumentKind is SqlAggregateArgumentKind.Expression or SqlAggregateArgumentKind.Raw;

    /// <summary>
    /// 使用结构化聚合描述初始化列。
    /// </summary>
    /// <param name="name">列名或聚合参数。</param>
    /// <param name="tableAlias">表别名。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="tableType">表实体类型。</param>
    /// <param name="raw">是否使用原始列文本。</param>
    /// <param name="aggregate">结构化聚合描述。</param>
    private ColumnItem(string name, string tableAlias, string columnAlias, Type tableType, bool raw,
        SqlAggregateDescriptor aggregate)
    {
        Name = name;
        TableAlias = tableAlias;
        ColumnAlias = columnAlias;
        TableType = tableType;
        Raw = raw;
        _aggregate = aggregate;
    }

    /// <summary>
    /// 创建普通结构化列。
    /// </summary>
    /// <param name="name">列名。</param>
    /// <param name="tableAlias">表别名。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="tableType">表实体类型。</param>
    /// <returns>普通列项。</returns>
    public static ColumnItem CreateColumn(string name, string tableAlias = null, string columnAlias = null,
        Type tableType = null) => new(name, tableAlias, columnAlias, tableType, false, null);

    /// <summary>
    /// 创建原始列。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <returns>原始列项。</returns>
    public static ColumnItem CreateRaw(string sql, string columnAlias = null) =>
        new(sql, null, columnAlias, null, true, null);

    /// <summary>
    /// 创建结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="name">列名。</param>
    /// <param name="tableAlias">表别名。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <param name="databaseName">数据库名称。</param>
    /// <param name="tableType">表实体类型。</param>
    /// <returns>结构化聚合列项。</returns>
    public static ColumnItem CreateAggregate(SqlAggregateFunction function, string name, string tableAlias = null,
        string columnAlias = null, bool distinct = false, string databaseName = null, Type tableType = null) =>
        new(name, tableAlias, columnAlias, tableType, false, new SqlAggregateDescriptor
        {
            Function = function,
            Distinct = distinct,
            ArgumentKind = SqlAggregateArgumentKind.Column,
            DatabaseName = databaseName
        });

    /// <summary>
    /// 创建通配符聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <returns>通配符聚合列项。</returns>
    public static ColumnItem CreateAggregateWildcard(SqlAggregateFunction function, string columnAlias = null) =>
        new("*", null, columnAlias, null, false, new SqlAggregateDescriptor
        {
            Function = function,
            ArgumentKind = SqlAggregateArgumentKind.Wildcard
        });

    /// <summary>
    /// 创建可转换表达式聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="expression">已转换的 SQL 表达式。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>表达式聚合列项。</returns>
    public static ColumnItem CreateAggregateExpression(SqlAggregateFunction function, string expression,
        string columnAlias = null, bool distinct = false) => new(expression, null, columnAlias, null, false,
        new SqlAggregateDescriptor { Function = function, Distinct = distinct, ArgumentKind = SqlAggregateArgumentKind.Expression });

    /// <summary>
    /// 创建原始 SQL 聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="sql">原始 SQL 参数。</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>原始聚合列项。</returns>
    public static ColumnItem CreateAggregateRaw(SqlAggregateFunction function, string sql, string columnAlias = null,
        bool distinct = false) => new(sql, null, columnAlias, null, false,
        new SqlAggregateDescriptor { Function = function, Distinct = distinct, ArgumentKind = SqlAggregateArgumentKind.Raw });

    /// <summary>
    /// 获取列名列表
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="register">实体别名注册器</param>
    /// <returns>按指定 SQL 方言和实体别名注册器渲染的列 SQL。</returns>
    public string ToSql(IDialect dialect, IEntityAliasRegister register)
    {
        if (AggregateFunction.HasValue)
            return GetAggregateSql(dialect, register);
        if (Raw)
            return dialect.GetColumn(Name, dialect.GetSafeName(ColumnAlias));
        var result = new SqlItem(Name, GetTableAlias(register), ColumnAlias, isResolve: false);
        return result.ToSql(dialect);
    }

    /// <summary>
    /// 获取结构化聚合 SQL。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="register">实体别名注册器。</param>
    /// <returns>聚合列 SQL。</returns>
    private string GetAggregateSql(IDialect dialect, IEntityAliasRegister register)
    {
        var argument = GetAggregateArgument(dialect, register);
        var distinct = AggregateDistinct ? "Distinct " : string.Empty;
        var column = $"{AggregateFunction.Value}({distinct}{argument})";
        return dialect.GetColumn(column, dialect.GetSafeName(ColumnAlias));
    }

    /// <summary>
    /// 获取聚合参数 SQL。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="register">实体别名注册器。</param>
    /// <returns>聚合参数 SQL。</returns>
    private string GetAggregateArgument(IDialect dialect, IEntityAliasRegister register)
    {
        if (AggregateWildcard)
            return "*";
        if (AggregateArgumentRaw)
            return Name;
        return GetStructuredAggregateArgument(dialect, register);
    }

    /// <summary>
    /// 获取结构化聚合列的方言标识符 SQL。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="register">实体别名注册器。</param>
    /// <returns>结构化聚合列 SQL。</returns>
    private string GetStructuredAggregateArgument(IDialect dialect, IEntityAliasRegister register)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(_aggregate?.DatabaseName) == false)
            result.Add(dialect.SafeName(_aggregate.DatabaseName));
        var tableAlias = GetTableAlias(register);
        if (string.IsNullOrWhiteSpace(tableAlias) == false)
            result.Add(dialect.SafeName(tableAlias));
        result.Add(dialect.SafeName(Name));
        return string.Join(".", result);
    }

    /// <summary>
    /// 获取表别名
    /// </summary>
    /// <param name="register">实体别名注册器</param>
    /// <returns>当前列使用的表别名；未指定别名且无法从注册器获取时返回 <see langword="null"/>。</returns>
    private string GetTableAlias(IEntityAliasRegister register) => string.IsNullOrWhiteSpace(TableAlias) == false
        ? TableAlias
        : register != null && register.Contains(TableType) ? register.GetAlias(TableType) : null;

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前列项的独立副本。</returns>
    public ColumnItem Clone()
    {
        return new ColumnItem(Name, TableAlias, ColumnAlias, TableType, Raw, _aggregate);
    }
}
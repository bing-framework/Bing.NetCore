using System.Text;
using System.Text.RegularExpressions;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql生成器基类
/// </summary>
public abstract class SqlBuilderBase : ISqlBuilder, ISqlPartAccessor, IUnionAccessor, ICteAccessor
{
    #region 字段

    /// <summary>
    /// 参数管理器
    /// </summary>
    private IParameterManager _parameterManager;

    /// <summary>
    /// 方言
    /// </summary>
    private IDialect _dialect;

    /// <summary>
    /// Select子句
    /// </summary>
    private ISelectClause _selectClause;

    /// <summary>
    /// From子句
    /// </summary>
    private IFromClause _fromClause;

    /// <summary>
    /// Join子句
    /// </summary>
    private IJoinClause _joinClause;

    /// <summary>
    /// Where子句
    /// </summary>
    private IWhereClause _whereClause;

    /// <summary>
    /// 分组字句
    /// </summary>
    private IGroupByClause _groupByClause;

    /// <summary>
    /// 排序子句
    /// </summary>
    private IOrderByClause _orderByClause;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private IParamLiteralsResolver _paramLiteralsResolver;

    /// <summary>
    /// 是否已添加过滤器
    /// </summary>
    private bool _isAddFilters;

    /// <summary>
    /// 已排除过滤器集合
    /// </summary>
    private List<Type> _excludedFilters;

    #endregion

    #region 属性

    /// <summary>
    /// 实体元数据解析器
    /// </summary>
    protected IEntityMetadata EntityMetadata { get; private set; }

    /// <summary>
    /// 表数据库
    /// </summary>
    protected ITableDatabase TableDatabase { get; private set; }

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    protected IEntityMappingResolver EntityMappingResolver { get; private set; }

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    protected IDatabaseContextAccessor DatabaseContextAccessor { get; private set; }

    /// <summary>
    /// Sql 参数工厂
    /// </summary>
    protected ISqlParameterFactory SqlParameterFactory { get; private set; }

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    protected SqlMetadataOptions MetadataOptions { get; private set; }

    /// <summary>
    /// 实体解析器
    /// </summary>
    protected IEntityResolver EntityResolver { get; private set; }

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    protected IEntityAliasRegister AliasRegister { get; private set; }

    /// <summary>
    /// 参数管理器
    /// </summary>
    public IParameterManager ParameterManager => _parameterManager ??= CreateParameterManager();

    /// <summary>
    /// Sql方言
    /// </summary>
    public IDialect Dialect => _dialect ??= GetDialect();

    /// <summary>
    /// Select子句
    /// </summary>
    public ISelectClause SelectClause => _selectClause ??= CreateSelectClause();

    /// <summary>
    /// From子句
    /// </summary>
    public IFromClause FromClause => _fromClause ??= CreateFromClause();

    /// <summary>
    /// Join子句
    /// </summary>
    public IJoinClause JoinClause => _joinClause ??= CreateJoinClause();

    /// <summary>
    /// Where子句
    /// </summary>
    public IWhereClause WhereClause => _whereClause ??= CreateWhereClause();

    /// <summary>
    /// 分组子句
    /// </summary>
    public IGroupByClause GroupByClause => _groupByClause ??= CreateGroupByClause();

    /// <summary>
    /// 排序子句
    /// </summary>
    public IOrderByClause OrderByClause => _orderByClause ??= CreateOrderByClause();

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    protected IParamLiteralsResolver ParamLiteralsResolver => _paramLiteralsResolver ??= GetParamLiteralsResolver();

    /// <summary>
    /// 跳过行数参数名
    /// </summary>
    protected string OffsetParam { get; private set; }

    /// <summary>
    /// 限制行数参数名
    /// </summary>
    protected string LimitParam { get; private set; }

    /// <summary>
    /// 分页
    /// </summary>
    public IPager Pager { get; private set; }

    /// <summary>
    /// 是否分组
    /// </summary>
    public bool IsGroup => GroupByClause.IsGroup;

    /// <summary>
    /// 是否限制行数
    /// </summary>
    protected bool IsLimit => string.IsNullOrWhiteSpace(LimitParam) == false;

    /// <summary>
    /// 是否包含联合操作
    /// </summary>
    public bool IsUnion => UnionItems.Count > 0;

    /// <summary>
    /// 联合操作项集合
    /// </summary>
    public List<BuilderItem> UnionItems { get; private set; }

    /// <summary>
    /// 公用表表达式CTE集合
    /// </summary>
    public List<BuilderItem> CteItems { get; private set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlBuilderBase"/>类型的实例
    /// </summary>
    /// <param name="metadata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    protected SqlBuilderBase(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null)
    {
        EntityMetadata = metadata;
        TableDatabase = tableDatabase;
        _parameterManager = parameterManager;
        MetadataOptions = metadataOptions ?? new SqlMetadataOptions();
        DatabaseContextAccessor = databaseContextAccessor;
        EntityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(metadata, databaseContextAccessor,
            MetadataOptions);
        SqlParameterFactory = sqlParameterFactory ?? new DefaultSqlParameterFactory(
            new DefaultFieldValueConverterSelector(null, MetadataOptions), databaseContextAccessor, MetadataOptions);
        EntityResolver = new EntityResolver(metadata);
        AliasRegister = new EntityAliasRegister();
        Pager = new Pager();
        UnionItems = new List<BuilderItem>();
        CteItems = new List<BuilderItem>();
        _excludedFilters = new List<Type>();
    }

    #endregion

    #region 工厂方法

    /// <summary>
    /// 创建参数管理器
    /// </summary>
    protected virtual IParameterManager CreateParameterManager() => new ParameterManager(Dialect);

    /// <summary>
    /// 获取Sql方言
    /// </summary>
    protected abstract IDialect GetDialect();

    /// <summary>
    /// 创建Select子句
    /// </summary>
    protected virtual ISelectClause CreateSelectClause() => new SelectClause(this, Dialect, EntityResolver, AliasRegister);

    /// <summary>
    /// 创建From子句
    /// </summary>
    protected virtual IFromClause CreateFromClause() => new FromClause(this, Dialect, EntityResolver, AliasRegister, TableDatabase);

    /// <summary>
    /// 创建Join子句
    /// </summary>
    protected virtual IJoinClause CreateJoinClause() => new JoinClause(this, Dialect, EntityResolver, AliasRegister, ParameterManager, TableDatabase);

    /// <summary>
    /// 创建Where子句
    /// </summary>
    protected virtual IWhereClause CreateWhereClause() => new WhereClause(this, Dialect, EntityResolver, AliasRegister,
        ParameterManager, null, EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions);

    /// <summary>
    /// 创建分组子句
    /// </summary>
    protected virtual IGroupByClause CreateGroupByClause() => new GroupByClause(Dialect, EntityResolver, AliasRegister);

    /// <summary>
    /// 创建排序子句
    /// </summary>
    protected virtual IOrderByClause CreateOrderByClause() => new OrderByClause(Dialect, EntityResolver, AliasRegister);

    /// <summary>
    /// 获取参数字面值解析器
    /// </summary>
    protected virtual IParamLiteralsResolver GetParamLiteralsResolver() => new ParamLiteralsResolver();

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    public abstract ISqlBuilder Clone();

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="sqlBuilder">源生成器</param>
    protected void Clone(SqlBuilderBase sqlBuilder)
    {
        if (sqlBuilder == null)
            throw new ArgumentNullException(nameof(sqlBuilder));

        EntityMetadata = sqlBuilder.EntityMetadata;
        _parameterManager = sqlBuilder._parameterManager?.Clone();
        MetadataOptions = sqlBuilder.MetadataOptions;
        DatabaseContextAccessor = sqlBuilder.DatabaseContextAccessor;
        EntityMappingResolver = sqlBuilder.EntityMappingResolver;
        SqlParameterFactory = sqlBuilder.SqlParameterFactory;
        EntityResolver = sqlBuilder.EntityResolver ?? new EntityResolver(EntityMetadata);
        AliasRegister = sqlBuilder.AliasRegister?.Clone() ?? new EntityAliasRegister();

        // 克隆各子句
        _selectClause = sqlBuilder._selectClause?.Clone(this, AliasRegister);
        _fromClause = sqlBuilder._fromClause?.Clone(this, AliasRegister);
        _joinClause = sqlBuilder._joinClause?.Clone(this, AliasRegister, _parameterManager);
        _whereClause = sqlBuilder._whereClause?.Clone(this, AliasRegister, _parameterManager);
        _groupByClause = sqlBuilder._groupByClause?.Clone(AliasRegister);
        _orderByClause = sqlBuilder._orderByClause?.Clone(AliasRegister);

        // 克隆分页信息
        Pager = sqlBuilder.Pager;
        OffsetParam = sqlBuilder.OffsetParam;
        LimitParam = sqlBuilder.LimitParam;

        // 克隆集合
        UnionItems = sqlBuilder.UnionItems.Select(t => new BuilderItem(t.Name, t.Builder.Clone())).ToList();
        CteItems = sqlBuilder.CteItems.Select(t => new BuilderItem(t.Name, t.Builder.Clone())).ToList();
        _excludedFilters = sqlBuilder._excludedFilters;
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    internal virtual DatabaseContext GetDatabaseContext() =>
        DatabaseContextAccessor?.Current ?? MetadataOptions?.DefaultDatabaseContext;

    /// <summary>
    /// 解析列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="propertyOrColumnName">属性名或列名</param>
    /// <returns>列映射元数据</returns>
    internal virtual ColumnMappingMetadata ResolveColumnMetadata(Type entityType, string propertyOrColumnName)
    {
        if (entityType == null || string.IsNullOrWhiteSpace(propertyOrColumnName) || EntityMappingResolver == null)
            return null;
        var mapping = EntityMappingResolver.Resolve(entityType, GetDatabaseContext());
        if (mapping?.Columns == null || mapping.Columns.Count == 0)
            return null;
        if (mapping.Columns.TryGetValue(propertyOrColumnName, out var column))
            return column;
        return mapping.Columns.Values.FirstOrDefault(t =>
            string.Equals(t.PropertyName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.ColumnName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 创建增强 Sql 参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="source">参数来源</param>
    /// <returns>Sql 参数</returns>
    internal virtual SqlParam CreateSqlParam(string name, object value, ColumnMappingMetadata column, Type entityType,
        SqlParameterSource source)
    {
        if (SqlParameterFactory == null)
            return null;
        return SqlParameterFactory.Create(name, value, column, GetDatabaseContext(), entityType, source);
    }

    #endregion

    #region Clear(清空)

    /// <summary>
    /// 清空并初始化
    /// </summary>
    public ISqlBuilder Clear()
    {
        AliasRegister = new EntityAliasRegister();
        ClearSelect();
        ClearFrom();
        ClearJoin();
        ClearWhere();
        ClearGroupBy();
        ClearOrderBy();
        ClearSqlParams();
        ClearPageParams();
        ClearUnionBuilders();
        ClearCte();
        return this;
    }

    /// <summary>
    /// 清空Select子句
    /// </summary>
    public ISqlBuilder ClearSelect()
    {
        _selectClause = CreateSelectClause();
        return this;
    }

    /// <summary>
    /// 清空From子句
    /// </summary>
    public ISqlBuilder ClearFrom()
    {
        _fromClause = CreateFromClause();
        return this;
    }

    /// <summary>
    /// 清空Join子句
    /// </summary>
    public ISqlBuilder ClearJoin()
    {
        _joinClause = CreateJoinClause();
        return this;
    }

    /// <summary>
    /// 清空Where子句
    /// </summary>
    public ISqlBuilder ClearWhere()
    {
        _isAddFilters = false;
        _whereClause = CreateWhereClause();
        return this;
    }

    /// <summary>
    /// 清空GroupBy子句
    /// </summary>
    public ISqlBuilder ClearGroupBy()
    {
        _groupByClause = CreateGroupByClause();
        return this;
    }

    /// <summary>
    /// 清空OrderBy子句
    /// </summary>
    public ISqlBuilder ClearOrderBy()
    {
        _orderByClause = CreateOrderByClause();
        return this;
    }

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    public ISqlBuilder ClearSqlParams()
    {
        _parameterManager.Clear();
        return this;
    }

    /// <summary>
    /// 清空分页参数
    /// </summary>
    public ISqlBuilder ClearPageParams()
    {
        Pager = new Pager();
        OffsetParam = null;
        LimitParam = null;
        return this;
    }

    /// <summary>
    /// 清空联合操作项
    /// </summary>
    public ISqlBuilder ClearUnionBuilders()
    {
        UnionItems = new List<BuilderItem>();
        return this;
    }

    /// <summary>
    /// 清空公用表表达式
    /// </summary>
    public ISqlBuilder ClearCte()
    {
        CteItems = new List<BuilderItem>();
        return this;
    }

    #endregion

    #region New(创建Sql生成器)

    /// <summary>
    /// 创建Sql生成器
    /// </summary>
    public abstract ISqlBuilder New();

    #endregion

    #region ToDebugSql(生成调试Sql语句)

    /// <summary>
    /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    public virtual string ToDebugSql() => GetDebugSql(ToSql());

    /// <summary>
    /// 获取调试Sql
    /// </summary>
    /// <param name="sql">Sql语句</param>
    private string GetDebugSql(string sql)
    {
        var parameters = ParameterManager.GetParams();
        foreach (var parameter in parameters)
            sql = Regex.Replace(sql, $@"{parameter.Key}\b", ParamLiteralsResolver.GetParamLiterals(parameter.Value));
        return sql;
    }

    #endregion

    #region ToSql(生成Sql)

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    public virtual string ToSql()
    {
        Init();
        Validate();
        var result = new StringBuilder();
        CreateSql(result);
        return result.ToString().Trim();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public virtual void Init() => OrderByClause.OrderBy(Pager?.Order);

    /// <summary>
    /// 验证
    /// </summary>
    public virtual void Validate()
    {
        FromClause.Validate();
        OrderByClause.Validate(IsLimit);
    }

    /// <summary>
    /// 创建Sql语句
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateSql(StringBuilder result)
    {
        // 创建CTE
        CreateCte(result);
        if (_isAddFilters == false)
            EnsureFiltersAdded();
        if (IsUnion)
        {
            CreateSqlByUnion(result);
            return;
        }
        CreateSqlByNoUnion(result);
    }

    /// <summary>
    /// 创建CTE
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateCte(StringBuilder result)
    {
        if (CteItems.Count == 0)
            return;

        var cte = new StringBuilder(CteItems.Count * 100);
        cte.Append($"{GetCteKeyWord()} ");

        for (var i = 0; i < CteItems.Count; i++)
        {
            var item = CteItems[i];
            cte.AppendLine($"{Dialect.SafeName(item.Name)} ");
            cte.Append($"As ({item.Builder.ToSql()})");

            if (i < CteItems.Count - 1)
                cte.AppendLine(",");
        }

        result.AppendLine(cte.ToString());
    }

    /// <summary>
    /// 获取CTE关键字
    /// </summary>
    protected virtual string GetCteKeyWord() => "With";

    /// <summary>
    /// 创建Sql语句 - 联合
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected void CreateSqlByUnion(StringBuilder result)
    {
        result.Append("(");
        AppendSelect(result);
        AppendFrom(result);
        AppendSql(result, JoinClause.ToSql());
        AppendSql(result, WhereClause.ToSql());
        AppendSql(result, GroupByClause.ToSql());
        AppendSql(result, ")");
        foreach (var operation in UnionItems)
        {
            AppendSql(result, operation.Name);
            AppendSql(result, $"({operation.Builder.ToSql()}");
            AppendSql(result, ")");
        }

        AppendSql(result, OrderByClause.ToSql());
        AppendLimit(result);
    }

    /// <summary>
    /// 创建Sql语句
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected void CreateSqlByNoUnion(StringBuilder result)
    {
        AppendSelect(result);
        AppendFrom(result);
        AppendSql(result, JoinClause.ToSql());
        AppendSql(result, WhereClause.ToSql());
        AppendSql(result, GroupByClause.ToSql());
        AppendSql(result, OrderByClause.ToSql());
        AppendLimit(result);
    }

    /// <summary>
    /// 添加Sql
    /// </summary>
    /// <param name="result">Sql拼接</param>
    /// <param name="sql">Sql语句</param>
    protected void AppendSql(StringBuilder result, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        result.AppendLine($"{sql} ");
    }

    /// <summary>
    /// 添加Select子句
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    protected virtual void AppendSelect(StringBuilder result)
    {
        var sql = SelectClause.ToSql();
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("必须设置Select子句");
        AppendSql(result, sql);
    }

    /// <summary>
    /// 添加From子句
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    protected virtual void AppendFrom(StringBuilder result)
    {
        var sql = FromClause.ToSql();
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("必须设置From子句");
        AppendSql(result, sql);
    }

    /// <summary>
    /// 确保过滤器已添加
    /// </summary>
    protected void EnsureFiltersAdded()
    {
        if (_isAddFilters)
            return;

        _isAddFilters = true;
        var context = new SqlContext(Dialect, AliasRegister, EntityMetadata, ParameterManager, this);
        foreach (var filter in SqlFilterCollection.Filters)
        {
            if (_excludedFilters.Count > 0 && _excludedFilters.Contains(filter.GetType()))
                continue;
            filter.Filter(context);
        }
    }

    /// <summary>
    /// 添加分页Sql
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    private void AppendLimit(StringBuilder result)
    {
        if (IsLimit)
            AppendSql(result, CreateLimitSql());
    }

    /// <summary>
    /// 创建分页Sql
    /// </summary>
    protected abstract string CreateLimitSql();

    #endregion

    #region GetCondition(获取查询条件)

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public virtual string GetCondition() => WhereClause.GetCondition();

    #endregion

    #region Pager(设置分页)

    /// <summary>
    /// 设置跳过行数
    /// </summary>
    /// <param name="count">跳过的行数</param>
    public ISqlBuilder Skip(int count)
    {
        var param = GetOffsetParam();
        ParameterManager.Add(param, count);
        return this;
    }

    /// <summary>
    /// 获取跳过行数的参数名
    /// </summary>
    protected string GetOffsetParam()
    {
        if (string.IsNullOrWhiteSpace(OffsetParam) == false)
            return OffsetParam;
        OffsetParam = ParameterManager.GenerateName();
        ParameterManager.Add(OffsetParam, 0);
        return OffsetParam;
    }

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="count">获取的行数</param>
    public ISqlBuilder Take(int count)
    {
        var param = GetLimitParam();
        ParameterManager.Add(param, count);
        Pager.PageSize = count;
        return this;
    }

    /// <summary>
    /// 获取限制行数的参数名
    /// </summary>
    protected string GetLimitParam()
    {
        if (string.IsNullOrWhiteSpace(LimitParam) == false)
            return LimitParam;
        LimitParam = ParameterManager.GenerateName();
        return LimitParam;
    }

    /// <summary>
    /// 设置分页
    /// </summary>
    /// <param name="pager">分页参数</param>
    public ISqlBuilder Page(IPager pager)
    {
        if (pager == null)
            return this;
        Pager = pager;
        Skip(pager.GetSkipCount()).Take(pager.PageSize);
        return this;
    }

    #endregion

    /// <summary>
    /// 忽略过滤器
    /// </summary>
    /// <typeparam name="TSqlFilter">Sql过滤器类型</typeparam>
    public virtual ISqlBuilder IgnoreFilter<TSqlFilter>() where TSqlFilter : ISqlFilter
    {
        var filterType = typeof(TSqlFilter);
        if (!_excludedFilters.Contains(filterType))
            _excludedFilters.Add(filterType);
        return this;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        builder.CheckNull(nameof(builder));
    }

    /// <summary>
    /// 添加Sql
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <param name="content">Sql子句</param>
    protected void AppendSql(StringBuilder builder, ISqlClause content)
    {
        if (content == null)
            return;
        if (content.Validate() == false)
            return;
        content.AppendTo(builder);
        builder.AppendLine(" ");
    }
}

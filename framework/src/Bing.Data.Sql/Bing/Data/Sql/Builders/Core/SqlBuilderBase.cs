using System.Text;
using System.Text.RegularExpressions;
using Bing.Data;
using Bing.Data.Enums;
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
public abstract class SqlBuilderBase : ISqlBuilder, ISqlCommonPartAccessor, ISqlQueryClauseAccessor, IUnionAccessor,
    ICteAccessor
{
    #region 字段

    /// <summary>
    /// 参数管理器
    /// </summary>
    private IParameterManager _parameterManager;

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

    /// <summary>
    /// 子查询参数重命名映射，确保重复渲染使用同一参数名称。
    /// </summary>
    private readonly Dictionary<ISqlBuilder, Dictionary<string, string>> _subqueryParameterNames =
        new(ReferenceComparer<ISqlBuilder>.Instance);

    #endregion

    #region 属性

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    protected IEntityMappingResolver EntityMappingResolver { get; private set; }

    /// <summary>
    /// SQL 提供程序。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// SQL Builder 可共享服务。
    /// </summary>
    protected SqlBuilderServices Services { get; private set; }

    /// <summary>
    /// 实体模型原始元数据提供器。
    /// </summary>
    protected IEntityModelMetadataProvider EntityModelMetadataProvider { get; private set; }

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    protected ISqlObjectNameFormatter ObjectNameFormatter { get; private set; }

    /// <summary>
    /// 跨数据库查询校验器。
    /// </summary>
    protected ISqlCrossDatabaseQueryValidator CrossDatabaseQueryValidator { get; private set; }

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected ISqlTableReferenceValidator TableReferenceValidator { get; private set; }

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
    /// Sql 配置
    /// </summary>
    protected SqlOptions Options { get; private set; }

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    protected ISqlDatabaseContextResolver DatabaseContextResolver { get; private set; }

    /// <summary>
    /// Builder 生命周期内固定的执行上下文。
    /// </summary>
    internal SqlBuilderExecutionContext ExecutionContext { get; private set; }

    /// <summary>
    /// 实体解析器
    /// </summary>
    protected IEntityResolver EntityResolver { get; private set; }

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    protected IEntityAliasRegister AliasRegister { get; private set; }

    /// <summary>
    /// 获取当前执行上下文解析后的数据库类型。
    /// </summary>
    protected DatabaseType ExecutionProviderDatabaseType => ExecutionContext.DatabaseType ?? Provider.DatabaseType;

    /// <summary>
    /// 参数管理器
    /// </summary>
    public IParameterManager ParameterManager => _parameterManager ??= CreateParameterManager();

    /// <summary>
    /// Sql方言
    /// </summary>
    public IDialect Dialect => Provider.Dialect;

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
    /// 使用 SQL 提供程序和共享服务初始化 SQL Builder。
    /// </summary>
    /// <param name="provider">SQL 提供程序。</param>
    /// <param name="services">可共享服务。</param>
    /// <param name="parameterManager">当前 Builder 的参数管理器。</param>
    protected SqlBuilderBase(ISqlProvider provider, SqlBuilderServices services,
        IParameterManager parameterManager = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _parameterManager = ApplyParameterLimit(parameterManager);
        Services = services ?? throw new ArgumentNullException(nameof(services));
        MetadataOptions = Services.MetadataOptions;
        Options = Services.Options;
        DatabaseContextAccessor = Services.DatabaseContextAccessor;
        DatabaseContextResolver = Services.DatabaseContextResolver;
        ExecutionContext = new SqlBuilderExecutionContext(DatabaseContextResolver.Resolve(Options) ??
            Options.GetDatabaseContext() ?? DatabaseContextAccessor?.Current ?? MetadataOptions.DefaultDatabaseContext);
        EntityModelMetadataProvider = Services.EntityModelMetadataProvider;
        EntityMappingResolver = Services.EntityMappingResolver;
        ObjectNameFormatter = Services.ObjectNameFormatter;
        CrossDatabaseQueryValidator = Services.CrossDatabaseQueryValidator;
        TableReferenceValidator = Services.TableReferenceValidator;
        SqlParameterFactory = Services.ParameterFactory;
        EntityResolver = new EntityResolver(EntityMappingResolver, DatabaseContextAccessor, MetadataOptions, Options,
            DatabaseContextResolver, EntityModelMetadataProvider, ExecutionContext.DatabaseContext);
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
    protected virtual IParameterManager CreateParameterManager()
    {
        var parameterManager = Provider.ParameterManagerFactory.Create(Dialect);
        if (parameterManager == null)
            throw new InvalidOperationException("SQL Provider 的参数管理器工厂返回了 null。");
        return ApplyParameterLimit(parameterManager);
    }

    /// <summary>
    /// 将参数管理器按当前 Provider 的参数数量限制进行幂等包装。
    /// </summary>
    /// <param name="parameterManager">待包装的参数管理器。</param>
    /// <returns>应用当前 Provider 参数数量限制后的参数管理器；输入为 null 时返回 null。</returns>
    private IParameterManager ApplyParameterLimit(IParameterManager parameterManager)
    {
        if (parameterManager == null || parameterManager is ParameterLimitManagerBase ||
            Provider is not ISqlParameterLimitProvider { MaxParameterCount: int maxParameterCount })
            return parameterManager;
        return parameterManager is IAdvancedParameterManager advancedParameterManager
            ? new AdvancedParameterLimitManager(advancedParameterManager, maxParameterCount, Provider.Key)
            : new ParameterLimitManager(parameterManager, maxParameterCount, Provider.Key);
    }

    /// <summary>
    /// 创建与当前 Builder 参数管理器配置一致的空实例。
    /// </summary>
    /// <remarks>
    /// 参数管理器必须创建同类型的空实例，且不得返回当前实例，避免 New 清空来源参数。
    /// </remarks>
    /// <returns>不含参数和值且序号已重置的独立参数管理器。</returns>
    protected IParameterManager CreateEmptyParameterManager()
    {
        var source = ParameterManager;
        var result = source.CreateEmpty();
        if (result == null)
            throw new InvalidOperationException("参数管理器创建空实例时返回了 null。");
        if (ReferenceEquals(source, result))
            throw new InvalidOperationException("参数管理器创建空实例时不能返回当前实例。");
        result.Clear();
        return result;
    }

    /// <summary>
    /// 创建绑定到当前 Builder 运行状态的子句上下文。
    /// </summary>
    /// <returns>当前运行依赖的子句上下文。</returns>
    protected SqlClauseContext CreateClauseContext() => new(this, Provider, EntityResolver, AliasRegister,
        ParameterManager, ExecutionContext, Services);

    /// <summary>
    /// 创建Select子句
    /// </summary>
    protected virtual ISelectClause CreateSelectClause() => Provider.ClauseFactory.CreateSelect(CreateClauseContext());

    /// <summary>
    /// 创建From子句
    /// </summary>
    protected virtual IFromClause CreateFromClause() => Provider.ClauseFactory.CreateFrom(CreateClauseContext());

    /// <summary>
    /// 创建Join子句
    /// </summary>
    protected virtual IJoinClause CreateJoinClause() => Provider.ClauseFactory.CreateJoin(CreateClauseContext());

    /// <summary>
    /// 创建Where子句
    /// </summary>
    protected virtual IWhereClause CreateWhereClause() => Provider.ClauseFactory.CreateWhere(CreateClauseContext());

    /// <summary>
    /// 创建分组子句
    /// </summary>
    protected virtual IGroupByClause CreateGroupByClause() => Provider.ClauseFactory.CreateGroupBy(CreateClauseContext());

    /// <summary>
    /// 创建排序子句
    /// </summary>
    protected virtual IOrderByClause CreateOrderByClause() => Provider.ClauseFactory.CreateOrderBy(CreateClauseContext());

    /// <summary>
    /// 获取参数字面值解析器
    /// </summary>
    protected virtual IParamLiteralsResolver GetParamLiteralsResolver() => Provider.ParamLiteralsResolver;

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    public virtual ISqlBuilder Clone()
    {
        var result = CreateBuilder(null);
        result.Clone(this);
        return result;
    }

    /// <summary>
    /// 创建与当前 Builder 使用相同 Provider 和服务的新实例。
    /// </summary>
    /// <param name="parameterManager">新 Builder 的参数管理器。</param>
    /// <returns>新的 Builder 实例。</returns>
    protected abstract SqlBuilderBase CreateBuilder(IParameterManager parameterManager);

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="sqlBuilder">源生成器</param>
    protected void Clone(SqlBuilderBase sqlBuilder)
    {
        if (sqlBuilder == null)
            throw new ArgumentNullException(nameof(sqlBuilder));

        if (sqlBuilder._parameterManager != null)
        {
            _parameterManager = sqlBuilder._parameterManager.Clone();
            if (_parameterManager == null)
                throw new InvalidOperationException("参数管理器克隆时返回了 null。");
            if (ReferenceEquals(_parameterManager, sqlBuilder._parameterManager))
                throw new InvalidOperationException("参数管理器克隆时不能返回当前实例。");
        }
        Services = sqlBuilder.Services;
        MetadataOptions = Services.MetadataOptions;
        Options = Services.Options;
        DatabaseContextAccessor = Services.DatabaseContextAccessor;
        DatabaseContextResolver = Services.DatabaseContextResolver;
        ExecutionContext = sqlBuilder.ExecutionContext;
        EntityMappingResolver = Services.EntityMappingResolver;
        EntityModelMetadataProvider = Services.EntityModelMetadataProvider;
        ObjectNameFormatter = Services.ObjectNameFormatter;
        CrossDatabaseQueryValidator = Services.CrossDatabaseQueryValidator;
        TableReferenceValidator = Services.TableReferenceValidator;
        SqlParameterFactory = Services.ParameterFactory;
        EntityResolver = new EntityResolver(EntityMappingResolver, DatabaseContextAccessor, MetadataOptions, Options,
            DatabaseContextResolver, EntityModelMetadataProvider, ExecutionContext.DatabaseContext);
        AliasRegister = sqlBuilder.AliasRegister?.Clone() ?? new EntityAliasRegister();
        var clonedContext = CreateClauseContext();

        // 克隆各子句
        _selectClause = sqlBuilder._selectClause?.Clone(clonedContext);
        _fromClause = sqlBuilder._fromClause?.Clone(clonedContext);
        _joinClause = sqlBuilder._joinClause?.Clone(clonedContext);
        _whereClause = sqlBuilder._whereClause?.Clone(clonedContext);
        _groupByClause = sqlBuilder._groupByClause?.Clone(clonedContext);
        _orderByClause = sqlBuilder._orderByClause?.Clone(clonedContext);

        // 克隆分页信息
        Pager = new Pager(sqlBuilder.Pager.Page, sqlBuilder.Pager.PageSize, sqlBuilder.Pager.TotalCount,
            sqlBuilder.Pager.Order);
        OffsetParam = sqlBuilder.OffsetParam;
        LimitParam = sqlBuilder.LimitParam;
        _isAddFilters = sqlBuilder._isAddFilters;

        // 克隆集合
        UnionItems = sqlBuilder.UnionItems.Select(t => new BuilderItem(t.Name, t.Builder.Clone())).ToList();
        CteItems = sqlBuilder.CteItems.Select(t => new BuilderItem(t.Name, t.Builder.Clone())).ToList();
        _excludedFilters = new List<Type>(sqlBuilder._excludedFilters);
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    internal virtual DatabaseContext GetDatabaseContext() => ExecutionContext.DatabaseContext;

    /// <summary>
    /// 渲染子查询并合并独立参数上下文。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    internal string RenderSubquery(ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var sql = builder.ToSql();
        return MergeSubqueryParameters(builder, sql);
    }

    /// <summary>
    /// 合并独立子查询参数，并在名称冲突时保持重命名结果稳定。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="sql">已经生成的子查询 SQL 或条件 SQL。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string MergeSubqueryParameters(ISqlBuilder builder, string sql)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (builder is not ISqlCommonPartAccessor accessor || ReferenceEquals(ParameterManager, accessor.ParameterManager))
            return sql;

        var sourceParameters = accessor.ParameterManager.GetParams();
        var sourceSqlParameters = (accessor.ParameterManager as IAdvancedParameterManager)?.GetSqlParams();
        if (_subqueryParameterNames.TryGetValue(builder, out var nameMap) == false)
        {
            nameMap = new Dictionary<string, string>();
            _subqueryParameterNames[builder] = nameMap;
        }
        foreach (var parameter in sourceParameters)
        {
            if (nameMap.TryGetValue(parameter.Key, out var targetName) == false)
            {
                targetName = parameter.Key;
                if (ParameterManager.Contains(parameter.Key) && Equals(ParameterManager.GetValue(parameter.Key), parameter.Value) == false)
                    targetName = ParameterManager.GenerateName();
                nameMap[parameter.Key] = targetName;
            }
            if (ParameterManager.Contains(targetName) == false)
            {
                if (sourceSqlParameters != null && sourceSqlParameters.TryGetValue(parameter.Key, out var sqlParameter) &&
                    ParameterManager is IAdvancedParameterManager advancedParameterManager)
                    advancedParameterManager.Add(CloneSqlParameter(sqlParameter, targetName));
                else
                    ParameterManager.Add(targetName, parameter.Value);
            }
            if (string.Equals(parameter.Key, targetName, StringComparison.Ordinal) == false)
                sql = Regex.Replace(sql, $@"(?<![\w]){Regex.Escape(parameter.Key)}(?![\w])", targetName);
        }
        return sql;
    }

    /// <summary>
    /// 克隆增强 SQL 参数并替换参数名。
    /// </summary>
    /// <param name="parameter">源参数。</param>
    /// <param name="name">目标参数名。</param>
    private static SqlParam CloneSqlParameter(SqlParam parameter, string name)
    {
        return new SqlParam(name, parameter.Value, parameter.DbType, parameter.Direction, parameter.Size,
            parameter.Precision, parameter.Scale)
        {
            OriginalValue = parameter.OriginalValue,
            EntityType = parameter.EntityType,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            DatabaseType = parameter.DatabaseType,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel,
            StorageKind = parameter.StorageKind,
            ConverterKind = parameter.ConverterKind,
            CustomConverterName = parameter.CustomConverterName
        };
    }

    /// <summary>
    /// 解析结构化对象名称使用的数据库类型。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>数据库类型。</returns>
    internal DatabaseType ResolveProviderDatabaseType(SqlTableReference reference = null)
    {
        return ExecutionContext.DatabaseType ?? Provider.DatabaseType;
    }

    /// <summary>
    /// 获取当前类型化 From 的结构化表引用。
    /// </summary>
    /// <returns>结构化表引用；原始字符串 From 返回 <see langword="null"/>。</returns>
    internal virtual SqlTableReference GetStructuredFromReference() =>
        (_fromClause as FromClause)?.GetStructuredReference();

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
        ParameterManager.Clear();
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
    public virtual ISqlBuilder New() => CreateBuilder(CreateEmptyParameterManager());

    #endregion

    #region ToDebugSql(生成调试Sql语句)

    /// <summary>
    /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    public virtual string ToDebugSql() => ToDebugSql(ToSql());

    /// <summary>
    /// 根据已生成的Sql语句生成调试Sql语句，Sql语句中的参数被替换为参数值
    /// </summary>
    /// <param name="sql">Sql语句</param>
    public virtual string ToDebugSql(string sql)
    {
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));
        var parameters = ParameterManager.GetParams();
        foreach (var parameter in parameters)
        {
            var literal = ParamLiteralsResolver.GetParamLiterals(parameter.Value);
            sql = Regex.Replace(sql, $@"(?<![\w]){Regex.Escape(parameter.Key)}(?![\w])", _ => literal);
        }
        return sql;
    }

    #endregion

    #region ToSql(生成Sql)

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    public virtual string ToSql()
    {
        var result = new StringBuilder(256);
        AppendTo(result);
        return result.ToString();
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
            cte.Append($"As ({RenderSubquery(item.Builder)})");

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
        AppendClause(result, JoinClause);
        AppendClause(result, WhereClause);
        AppendClause(result, GroupByClause);
        AppendSql(result, ")");
        foreach (var operation in UnionItems)
        {
            AppendSql(result, operation.Name);
            AppendSql(result, $"({RenderSubquery(operation.Builder)}");
            AppendSql(result, ")");
        }

        AppendClause(result, OrderByClause);
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
        AppendClause(result, JoinClause);
        AppendClause(result, WhereClause);
        AppendClause(result, GroupByClause);
        AppendClause(result, OrderByClause);
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
    /// 追加 SQL 子句，并保持查询 Builder 的既有换行布局。
    /// </summary>
    /// <param name="result">最终 SQL 缓冲区。</param>
    /// <param name="clause">待输出的 SQL 子句。</param>
    protected void AppendClause(StringBuilder result, ISqlContent clause)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (clause == null)
            return;
        var startIndex = result.Length;
        clause.AppendTo(result);
        if (result.Length == startIndex)
            return;
        result.AppendLine(" ");
    }

    /// <summary>
    /// 添加Select子句
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    protected virtual void AppendSelect(StringBuilder result)
    {
        var startIndex = result.Length;
        SelectClause.AppendTo(result);
        if (result.Length == startIndex)
            throw new InvalidOperationException("必须设置Select子句");
        result.AppendLine(" ");
    }

    /// <summary>
    /// 添加From子句
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    protected virtual void AppendFrom(StringBuilder result)
    {
        var startIndex = result.Length;
        FromClause.AppendTo(result);
        if (result.Length == startIndex)
            throw new InvalidOperationException("必须设置From子句");
        result.AppendLine(" ");
    }

    /// <summary>
    /// 确保过滤器已添加
    /// </summary>
    protected void EnsureFiltersAdded()
    {
        if (_isAddFilters)
            return;

        _isAddFilters = true;
        var context = new SqlFilterContext(Dialect, AliasRegister, ParameterManager, this, Services,
            ExecutionContext.DatabaseContext);
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
    protected virtual string CreateLimitSql() => Provider.PaginationRenderer.Render(GetOffsetParam(), GetLimitParam());

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
        var startIndex = builder.Length;
        Init();
        Validate();
        CreateSql(builder);
        TrimAppendedWhitespace(builder, startIndex);
    }

    /// <summary>
    /// 移除本次 SQL 渲染追加片段末尾的空白字符。
    /// </summary>
    /// <param name="builder">字符串生成器。</param>
    /// <param name="startIndex">本次追加前的起始位置。</param>
    private static void TrimAppendedWhitespace(StringBuilder builder, int startIndex)
    {
        var length = builder.Length;
        while (length > startIndex && char.IsWhiteSpace(builder[length - 1]))
            length--;
        if (length < builder.Length)
            builder.Remove(length, builder.Length - length);
    }

}

/// <summary>
/// 按对象实例比较引用的比较器。
/// </summary>
/// <typeparam name="T">引用类型。</typeparam>
internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    /// <summary>
    /// 比较器实例。
    /// </summary>
    public static ReferenceComparer<T> Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    /// <inheritdoc />
    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

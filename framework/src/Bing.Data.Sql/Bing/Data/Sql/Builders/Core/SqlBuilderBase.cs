using System.Text;
using System.Text.RegularExpressions;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql生成器基类
/// </summary>
public abstract partial class SqlBuilderBase : ISqlBuilder, ISqlCommonPartAccessor, ISqlQueryClauseAccessor, IUnionAccessor,
    ICteAccessor, ISqlOperationStateManager, IReturningClauseAccessor
{

    /// <summary>
    /// 是否已配置会输出的行限制。
    /// </summary>
    internal bool HasLimit => IsLimit;

    /// <summary>
    /// 是否需要通过独立渲染快照应用动态数据边界。
    /// </summary>
    internal bool RequiresRenderSnapshot => ShouldRenderFromSnapshot();
    #region 字段

    /// <summary>
    /// 参数管理器
    /// </summary>
    private IParameterManager _parameterManager;

    /// <summary>
    /// Builder 子句共享的可替换参数状态引用。
    /// </summary>
    private readonly ParameterManagerState _parameterManagerState;

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
    /// 统一 Mutation 子句共享的执行上下文。
    /// </summary>
    private SqlMutationContext _mutationContext;

    /// <summary>
    /// 当前 Provider 的 Mutation 子句工厂缓存。
    /// </summary>
    private ISqlMutationClauseFactory _mutationClauseFactory;

    /// <summary>
    /// Insert 子句的延迟创建缓存。
    /// </summary>
    private IInsertClause _insertClause;

    /// <summary>
    /// Insert 列集合子句的延迟创建缓存。
    /// </summary>
    private IInsertColumnsClause _insertColumnsClause;

    /// <summary>
    /// Insert Values 子句的延迟创建缓存。
    /// </summary>
    private IValuesClause _valuesClause;

    /// <summary>
    /// Update 子句的延迟创建缓存。
    /// </summary>
    private IUpdateClause _updateClause;

    /// <summary>
    /// Update From 子句的延迟创建缓存，并在克隆时复制其来源表。
    /// </summary>
    private IUpdateFromClause _updateFromClause;

    /// <summary>
    /// Update Set 子句的延迟创建缓存。
    /// </summary>
    private ISetClause _setClause;

    /// <summary>
    /// Delete 子句的延迟创建缓存。
    /// </summary>
    private IDeleteClause _deleteClause;

    /// <summary>
    /// Delete Using 子句的延迟创建缓存，并在克隆时复制其来源表。
    /// </summary>
    private IDeleteUsingClause _deleteUsingClause;

    /// <summary>
    /// Mutation Where 子句的延迟创建缓存。
    /// </summary>
    private IMutationWhereClause _mutationWhereClause;

    /// <summary>
    /// 是否已将默认写入数据边界追加到当前 Mutation Where 子句。
    /// </summary>
    private bool _isMutationDataBoundaryApplied;

    /// <summary>
    /// Returning 子句的延迟创建缓存，并在克隆时复制返回列。
    /// </summary>
    private IReturningClause _returningClause;

    /// <summary>
    /// 当前 Builder 的查询或 Mutation 构造阶段，用于拒绝不兼容的 Fluent 调用。
    /// </summary>
    private SqlBuilderOperationState _operationState;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private IParamLiteralsResolver _paramLiteralsResolver;

    /// <summary>
    /// 是否已添加过滤器
    /// </summary>
    private bool _isAddFilters;

    /// <summary>
    /// 当前查询引用的类型化子查询父上下文。
    /// </summary>
    private string _parentQueryContextId;

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
    public IParameterManager ParameterManager
    {
        get
        {
            if (_parameterManager == null)
            {
                _parameterManager = CreateParameterManager();
                _parameterManagerState.Current = _parameterManager;
            }
            return _parameterManager;
        }
    }

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

    /// <inheritdoc />
    public SqlOperationKind OperationKind => _operationState switch
    {
        SqlBuilderOperationState.Select => SqlOperationKind.Select,
        SqlBuilderOperationState.InsertValues => SqlOperationKind.InsertValues,
        SqlBuilderOperationState.InsertSelect => SqlOperationKind.InsertSelect,
        SqlBuilderOperationState.Update => SqlOperationKind.Update,
        SqlBuilderOperationState.Delete => SqlOperationKind.Delete,
        _ => SqlOperationKind.None
    };

    /// <summary>
    /// 恢复 Join 候选失败前的参数状态。
    /// </summary>
    /// <param name="snapshot">参数快照。</param>
    internal void RestoreParameterManager(IParameterManager snapshot)
    {
        ReplaceParameterManager(snapshot);
    }

    /// <summary>
    /// 以一次状态替换提交参数管理器，避免调用第三方管理器的部分写入操作。
    /// </summary>
    /// <param name="parameterManager">新的完整参数状态。</param>
    internal void ReplaceParameterManager(IParameterManager parameterManager)
    {
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        _parameterManager = parameterManager;
        _parameterManagerState.Current = parameterManager;
    }

    /// <summary>
    /// 恢复 Join 候选失败前的操作状态。
    /// </summary>
    /// <param name="operationKind">操作类型。</param>
    internal void RestoreOperationState(SqlOperationKind operationKind)
    {
        _operationState = operationKind switch
        {
            SqlOperationKind.Select => SqlBuilderOperationState.Select,
            SqlOperationKind.InsertValues => SqlBuilderOperationState.InsertValues,
            SqlOperationKind.InsertSelect => SqlBuilderOperationState.InsertSelect,
            SqlOperationKind.Update => SqlBuilderOperationState.Update,
            SqlOperationKind.Delete => SqlBuilderOperationState.Delete,
            _ => SqlBuilderOperationState.None
        };
    }

    /// <inheritdoc />
    public SqlMutationContext MutationContext => _mutationContext ??= CreateMutationContext();

    /// <inheritdoc />
    public IInsertClause InsertClause => _insertClause ??= MutationClauseFactory.CreateInsert(MutationContext);

    /// <inheritdoc />
    public IInsertColumnsClause InsertColumnsClause =>
        _insertColumnsClause ??= MutationClauseFactory.CreateInsertColumns(MutationContext);

    /// <inheritdoc />
    public IValuesClause ValuesClause => _valuesClause ??= MutationClauseFactory.CreateValues(MutationContext);

    /// <inheritdoc />
    public IUpdateClause UpdateClause => _updateClause ??= MutationClauseFactory.CreateUpdate(MutationContext);

    /// <inheritdoc />
    public IUpdateFromClause UpdateFromClause => _updateFromClause ??= CreateUpdateFromClause();

    /// <inheritdoc />
    public ISetClause SetClause => _setClause ??= MutationClauseFactory.CreateSet(MutationContext);

    /// <inheritdoc />
    public IDeleteClause DeleteClause => _deleteClause ??= MutationClauseFactory.CreateDelete(MutationContext);

    /// <inheritdoc />
    public IDeleteUsingClause DeleteUsingClause => _deleteUsingClause ??= CreateDeleteUsingClause();

    /// <inheritdoc />
    public IReturningClause ReturningClause => _returningClause ??= CreateReturningClause();

    /// <inheritdoc />
    IMutationWhereClause IMutationWhereClauseAccessor.WhereClause =>
        _mutationWhereClause ??= MutationClauseFactory.CreateWhere(MutationContext);

    /// <summary>
    /// 是否显式允许无条件 Update/Delete。
    /// </summary>
    protected bool AllowAllRows { get; private set; }

    private ISqlMutationClauseFactory MutationClauseFactory => _mutationClauseFactory ??=
        (Provider as ISqlMutationClauseFactoryProvider)?.MutationClauseFactory ?? new DefaultSqlMutationClauseFactory();

    /// <summary>
    /// 创建 Update From 子句，优先使用 Provider 的可选专用工厂。
    /// </summary>
    /// <returns>Provider 专用或默认的 Update From 子句。</returns>
    private IUpdateFromClause CreateUpdateFromClause() => MutationClauseFactory is ISqlUpdateFromClauseFactory factory
        ? factory.CreateUpdateFrom(MutationContext)
        : new UpdateFromClause(MutationContext);

    /// <summary>
    /// 创建 Delete Using 子句，优先使用 Provider 的可选专用工厂。
    /// </summary>
    /// <returns>Provider 专用或默认的 Delete Using 子句。</returns>
    private IDeleteUsingClause CreateDeleteUsingClause() => MutationClauseFactory is ISqlDeleteUsingClauseFactory factory
        ? factory.CreateDeleteUsing(MutationContext)
        : new DeleteUsingClause(MutationContext);

    /// <summary>
    /// 创建 Returning 子句，优先使用 Provider 的可选专用工厂。
    /// </summary>
    /// <returns>Provider 专用或默认的 Returning 子句。</returns>
    private IReturningClause CreateReturningClause() => MutationClauseFactory is ISqlReturningClauseFactory factory
        ? factory.CreateReturning(MutationContext)
        : new ReturningClause(MutationContext);

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

    /// <summary>
    /// Builder 创建时冻结的查询语法能力。
    /// </summary>
    private SqlQueryCapabilities QueryCapabilities { get; set; }

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
        _parameterManagerState = new ParameterManagerState { Current = _parameterManager };
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
        QueryCapabilities = ResolveQueryCapabilities();
        AliasRegister = new EntityAliasRegister();
        Pager = new Pager();
        UnionItems = new List<BuilderItem>();
        CteItems = new List<BuilderItem>();
        _mutationContext = CreateMutationContext();
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
        var maxParameterCount = SqlProviderCapabilityResolver.GetProfile(Provider).Limits.MaxParameterCount;
        if (parameterManager == null || parameterManager is ParameterLimitManagerBase || maxParameterCount == null)
            return parameterManager;
        return parameterManager is IAdvancedParameterManager advancedParameterManager
            ? new AdvancedParameterLimitManager(advancedParameterManager, maxParameterCount.Value, Provider.Key)
            : new ParameterLimitManager(parameterManager, maxParameterCount.Value, Provider.Key);
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
    protected SqlClauseContext CreateClauseContext()
    {
        _ = ParameterManager;
        return new SqlClauseContext(this, Provider, EntityResolver, AliasRegister, _parameterManagerState,
            ExecutionContext, Services);
    }

    private SqlMutationContext CreateMutationContext()
    {
        var result = new SqlMutationContext(Provider, ParameterManager, Services, ExecutionContext)
        {
            OperationStateManager = this
        };
        return result;
    }

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
        _parameterManagerState.Current = _parameterManager;
        Services = sqlBuilder.Services;
        MetadataOptions = Services.MetadataOptions;
        Options = Services.Options;
        DatabaseContextAccessor = Services.DatabaseContextAccessor;
        DatabaseContextResolver = Services.DatabaseContextResolver;
        ExecutionContext = sqlBuilder.ExecutionContext;
        QueryCapabilities = sqlBuilder.QueryCapabilities;
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
        _mutationClauseFactory = sqlBuilder._mutationClauseFactory;
        _mutationContext = CreateMutationContext();
        _operationState = sqlBuilder._operationState;
        AllowAllRows = sqlBuilder.AllowAllRows;

        // 克隆各子句
        _selectClause = sqlBuilder._selectClause?.Clone(clonedContext);
        _fromClause = sqlBuilder._fromClause?.Clone(clonedContext);
        _joinClause = sqlBuilder._joinClause?.Clone(clonedContext);
        _whereClause = sqlBuilder._whereClause?.Clone(clonedContext);
        _groupByClause = sqlBuilder._groupByClause?.Clone(clonedContext);
        _orderByClause = sqlBuilder._orderByClause?.Clone(clonedContext);
        _insertClause = sqlBuilder._insertClause?.Clone(MutationContext);
        _insertColumnsClause = sqlBuilder._insertColumnsClause?.Clone(MutationContext);
        _valuesClause = sqlBuilder._valuesClause?.Clone(MutationContext);
        _updateClause = sqlBuilder._updateClause?.Clone(MutationContext);
        _updateFromClause = sqlBuilder._updateFromClause?.Clone(MutationContext);
        _setClause = sqlBuilder._setClause?.Clone(MutationContext);
        _deleteClause = sqlBuilder._deleteClause?.Clone(MutationContext);
        _deleteUsingClause = sqlBuilder._deleteUsingClause?.Clone(MutationContext);
        _mutationWhereClause = sqlBuilder._mutationWhereClause?.Clone(MutationContext);
        _returningClause = sqlBuilder._returningClause?.Clone(MutationContext);
        _isMutationDataBoundaryApplied = sqlBuilder._isMutationDataBoundaryApplied;

        // 克隆分页信息
        Pager = new Pager(sqlBuilder.Pager.Page, sqlBuilder.Pager.PageSize, sqlBuilder.Pager.TotalCount,
            sqlBuilder.Pager.Order, sqlBuilder.Pager is Pager sourcePager && sourcePager.IsTotalCountKnown);
        OffsetParam = sqlBuilder.OffsetParam;
        LimitParam = sqlBuilder.LimitParam;
        _isAddFilters = sqlBuilder._isAddFilters;
        _parentQueryContextId = sqlBuilder._parentQueryContextId;

        // 克隆集合
        UnionItems = CloneBuilderItems(sqlBuilder.UnionItems, sqlBuilder);
        CteItems = CloneBuilderItems(sqlBuilder.CteItems, sqlBuilder);
    }

    /// <summary>
    /// 克隆集合操作 Builder，并保留已冻结的子查询参数重命名关系。
    /// </summary>
    /// <param name="items">待克隆的集合操作项。</param>
    /// <param name="source">当前克隆的源 Builder。</param>
    /// <returns>独立的集合操作项。</returns>
    private List<BuilderItem> CloneBuilderItems(IEnumerable<BuilderItem> items, SqlBuilderBase source)
    {
        var result = new List<BuilderItem>();
        foreach (var item in items)
        {
            var builder = item.Builder.Clone();
            result.Add(new BuilderItem(item.Name, builder));
            if (source._subqueryParameterNames.TryGetValue(item.Builder, out var names))
                _subqueryParameterNames[builder] = new Dictionary<string, string>(names);
        }
        return result;
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    internal virtual DatabaseContext GetDatabaseContext() => ExecutionContext.DatabaseContext;

    /// <summary>
    /// 获取当前 Builder 冻结连接对应的物理数据库身份。
    /// </summary>
    /// <returns>不含凭据的物理数据库身份；无法取得连接字符串时返回 null。</returns>
    internal SqlDatabaseIdentity GetDatabaseIdentity()
    {
        var context = GetDatabaseContext();
        var connectionString = Options?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;
        var databaseType = context?.DataSource?.DatabaseType ?? Options?.DatabaseType ?? Provider.DatabaseType;
        return Services.DatabaseIdentityResolver.Resolve(databaseType, connectionString);
    }

    /// <summary>
    /// 获取可识别同一根查询执行上下文的内部作用域令牌。
    /// </summary>
    /// <returns>当前根查询专属的 SQL 选项实例。</returns>
    internal object GetExecutionScope() => Options;

    /// <summary>
    /// 当前 Builder 引用的类型化子查询父上下文。
    /// </summary>
    internal string ParentQueryContextId => _parentQueryContextId;

    /// <summary>
    /// 记录类型化子查询的父上下文，重复引用时保留最外层父关系。
    /// </summary>
    internal void RegisterSubqueryParent(string parentQueryContextId)
    {
        if (string.IsNullOrWhiteSpace(_parentQueryContextId) && string.IsNullOrWhiteSpace(parentQueryContextId) == false)
            _parentQueryContextId = parentQueryContextId;
    }

    /// <summary>
    /// 渲染子查询并合并独立参数上下文。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    protected internal string RenderSubquery(ISqlBuilder builder)
    {
        return RenderSubquery(builder, ParameterManager, _subqueryParameterNames);
    }

    /// <summary>
    /// 使用指定的候选参数和重命名映射渲染子查询。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="parameterManager">接收子查询参数的候选管理器。</param>
    /// <param name="subqueryParameterNames">接收子查询重命名结果的候选映射。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string RenderSubquery(ISqlBuilder builder, IParameterManager parameterManager,
        IDictionary<ISqlBuilder, Dictionary<string, string>> subqueryParameterNames)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        if (subqueryParameterNames == null)
            throw new ArgumentNullException(nameof(subqueryParameterNames));
        var snapshot = builder.Clone();
        var sql = snapshot is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSnapshot() : snapshot.ToSql();
        return MergeSubqueryParameters(builder, snapshot, sql, parameterManager, subqueryParameterNames);
    }

    /// <summary>
    /// 合并独立子查询参数，并在名称冲突时保持重命名结果稳定。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="sql">已经生成的子查询 SQL 或条件 SQL。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string MergeSubqueryParameters(ISqlBuilder builder, string sql)
    {
        return MergeSubqueryParameters(builder, builder, sql, ParameterManager, _subqueryParameterNames);
    }

    /// <summary>
    /// 合并指定渲染快照中的子查询参数，并使用源 Builder 记录稳定的重命名结果。
    /// </summary>
    /// <param name="builder">原始子查询生成器，用于缓存参数重命名关系。</param>
    /// <param name="parameterSource">与 SQL 同一渲染快照的参数来源。</param>
    /// <param name="sql">已经生成的子查询 SQL 或条件 SQL。</param>
    /// <param name="parameterManager">接收子查询参数的目标管理器。</param>
    /// <param name="subqueryParameterNames">接收子查询重命名结果的目标映射。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    private string MergeSubqueryParameters(ISqlBuilder builder, ISqlBuilder parameterSource, string sql,
        IParameterManager parameterManager,
        IDictionary<ISqlBuilder, Dictionary<string, string>> subqueryParameterNames)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        if (subqueryParameterNames == null)
            throw new ArgumentNullException(nameof(subqueryParameterNames));
        if (parameterSource is not ISqlCommonPartAccessor accessor ||
            ReferenceEquals(parameterManager, accessor.ParameterManager))
            return sql;

        var sourceParameters = accessor.ParameterManager.GetParams();
        var sourceSqlParameters = (accessor.ParameterManager as IAdvancedParameterManager)?.GetSqlParams();
        var nameMap = subqueryParameterNames.TryGetValue(builder, out var existingNameMap)
            ? new Dictionary<string, string>(existingNameMap)
            : new Dictionary<string, string>();
        var parameterProbe = parameterManager.Clone();
        foreach (var parameter in sourceParameters)
        {
            if (nameMap.TryGetValue(parameter.Key, out var targetName) == false)
            {
                targetName = parameter.Key;
                if (parameterProbe.Contains(parameter.Key) && Equals(parameterProbe.GetValue(parameter.Key), parameter.Value) == false)
                    targetName = parameterProbe.GenerateName();
                nameMap[parameter.Key] = targetName;
            }
            AddSubqueryParameter(parameterProbe, sourceSqlParameters, parameter.Key, targetName, parameter.Value);
        }
        foreach (var parameter in sourceParameters)
            AddSubqueryParameter(parameterManager, sourceSqlParameters, parameter.Key, nameMap[parameter.Key], parameter.Value);
        subqueryParameterNames[builder] = nameMap;
        return ReplaceParameterTokens(sql, nameMap);
    }

    /// <summary>
    /// 创建当前子查询参数重命名映射的候选副本。
    /// </summary>
    /// <returns>独立的子查询参数重命名映射。</returns>
    internal Dictionary<ISqlBuilder, Dictionary<string, string>> CloneSubqueryParameterNames() =>
        _subqueryParameterNames.ToDictionary(item => item.Key,
            item => new Dictionary<string, string>(item.Value), ReferenceComparer<ISqlBuilder>.Instance);

    /// <summary>
    /// 以一次状态替换子查询参数重命名映射。
    /// </summary>
    /// <param name="subqueryParameterNames">新的完整映射。</param>
    internal void ReplaceSubqueryParameterNames(
        IReadOnlyDictionary<ISqlBuilder, Dictionary<string, string>> subqueryParameterNames)
    {
        if (subqueryParameterNames == null)
            throw new ArgumentNullException(nameof(subqueryParameterNames));
        _subqueryParameterNames.Clear();
        foreach (var item in subqueryParameterNames)
            _subqueryParameterNames[item.Key] = new Dictionary<string, string>(item.Value);
    }

    /// <summary>
    /// 在派生表渲染期间执行操作；操作失败时恢复参数和参数重命名映射。
    /// </summary>
    /// <remarks>
    /// 派生表渲染会把独立 Builder 的参数合并到当前 Builder。复合操作应通过本方法包裹其候选阶段，
    /// 以避免后续验证或表达式解析失败后遗留参数状态。
    /// </remarks>
    /// <param name="action">包含派生表渲染的候选操作。</param>
    internal void ExecuteWithSubqueryRenderRollback(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var parameterSnapshot = ParameterManager.Clone();
        var parameterNameSnapshot = _subqueryParameterNames.ToDictionary(item => item.Key,
            item => new Dictionary<string, string>(item.Value), ReferenceComparer<ISqlBuilder>.Instance);
        try
        {
            action();
        }
        catch
        {
            RestoreQueryRenderSnapshot(parameterSnapshot, parameterNameSnapshot);
            throw;
        }
    }

    /// <summary>
    /// 将子查询参数复制到目标参数管理器；同名参数已存在时保留原值。
    /// </summary>
    /// <param name="target">目标参数管理器。</param>
    /// <param name="sourceSqlParameters">源参数元数据快照。</param>
    /// <param name="sourceName">源参数名称。</param>
    /// <param name="targetName">目标参数名称。</param>
    /// <param name="value">源参数值。</param>
    private static void AddSubqueryParameter(IParameterManager target,
        IReadOnlyDictionary<string, SqlParam> sourceSqlParameters, string sourceName, string targetName, object value)
    {
        if (target.Contains(targetName))
            return;
        if (sourceSqlParameters != null && sourceSqlParameters.TryGetValue(sourceName, out var sqlParameter) &&
            target is IAdvancedParameterManager advancedParameterManager)
        {
            advancedParameterManager.Add(CloneSqlParameter(sqlParameter, targetName));
            return;
        }
        target.Add(targetName, value);
    }

    /// <summary>
    /// 在 SQL 代码片段中替换独立参数标记，同时保留字符串、注释和标识符中的原始文本。
    /// </summary>
    /// <param name="sql">待处理的 SQL 文本。</param>
    /// <param name="parameterNames">源参数名称与目标参数名称的映射，均包含方言前缀。</param>
    /// <returns>仅替换实际参数标记后的 SQL 文本。</returns>
    private static string ReplaceParameterTokens(string sql, IReadOnlyDictionary<string, string> parameterNames)
    {
        if (string.IsNullOrEmpty(sql) || parameterNames == null || parameterNames.Count == 0)
            return sql;

        var replacementTrie = ParameterTokenReplacementTrie.Create(parameterNames);
        if (replacementTrie == null)
            return sql;

        var result = new StringBuilder(sql.Length);
        for (var index = 0; index < sql.Length;)
        {
            var current = sql[index];
            if (current == '\'')
            {
                index = AppendQuotedSegment(sql, result, index, '\'');
                continue;
            }
            if (current == '"')
            {
                index = AppendQuotedSegment(sql, result, index, '"');
                continue;
            }
            if (current == '`')
            {
                index = AppendQuotedSegment(sql, result, index, '`');
                continue;
            }
            if (current == '[')
            {
                index = AppendBracketedIdentifier(sql, result, index);
                continue;
            }
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = AppendLineComment(sql, result, index);
                continue;
            }
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = AppendBlockComment(sql, result, index);
                continue;
            }
            if (current == '$' && TryAppendDollarQuotedSegment(sql, result, ref index))
                continue;
            if (replacementTrie.TryGetValue(sql, index, out var parameterName, out var replacement) &&
                IsParameterToken(sql, index, parameterName))
            {
                result.Append(replacement);
                index += parameterName.Length;
                continue;
            }
            result.Append(current);
            index++;
        }
        return result.ToString();
    }

    /// <summary>
    /// 替换单个 SQL 参数标记。
    /// </summary>
    /// <param name="sql">待处理的 SQL 文本。</param>
    /// <param name="sourceName">源参数名称，包含方言前缀。</param>
    /// <param name="targetName">目标参数名称或调试文本。</param>
    /// <returns>仅替换实际参数标记后的 SQL 文本。</returns>
    private static string ReplaceParameterToken(string sql, string sourceName, string targetName) =>
        ReplaceParameterTokens(sql, new Dictionary<string, string> { [sourceName] = targetName });

    /// <summary>
    /// 参数标记替换 Trie。
    /// </summary>
    /// <remarks>
    /// 将参数名匹配限制为 SQL 文本的一次前向扫描，避免调试 SQL 按参数数量重复遍历整段语句。
    /// </remarks>
    private sealed class ParameterTokenReplacementTrie
    {
        /// <summary>
        /// 子节点。
        /// </summary>
        private readonly Dictionary<char, ParameterTokenReplacementTrie> _children = new();

        /// <summary>
        /// 当前节点对应的完整参数名。
        /// </summary>
        private string _parameterName;

        /// <summary>
        /// 当前节点对应的替换文本。
        /// </summary>
        private string _replacement;

        /// <summary>
        /// 从参数替换映射创建 Trie。
        /// </summary>
        /// <param name="replacements">参数名与替换文本的映射。</param>
        /// <returns>至少包含一项有效替换时返回 Trie，否则返回 null。</returns>
        public static ParameterTokenReplacementTrie Create(IReadOnlyDictionary<string, string> replacements)
        {
            if (replacements == null || replacements.Count == 0)
                return null;
            var result = new ParameterTokenReplacementTrie();
            var hasReplacement = false;
            foreach (var replacement in replacements)
            {
                if (string.IsNullOrWhiteSpace(replacement.Key) ||
                    string.Equals(replacement.Key, replacement.Value, StringComparison.Ordinal))
                    continue;
                var node = result;
                foreach (var character in replacement.Key)
                {
                    if (node._children.TryGetValue(character, out var child) == false)
                    {
                        child = new ParameterTokenReplacementTrie();
                        node._children[character] = child;
                    }
                    node = child;
                }
                node._parameterName = replacement.Key;
                node._replacement = replacement.Value;
                hasReplacement = true;
            }
            return hasReplacement ? result : null;
        }

        /// <summary>
        /// 获取当前位置最长匹配的参数替换项。
        /// </summary>
        /// <param name="sql">完整 SQL 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <param name="parameterName">匹配的完整参数名。</param>
        /// <param name="replacement">对应替换文本。</param>
        /// <returns>存在匹配项时返回 <see langword="true"/>。</returns>
        public bool TryGetValue(string sql, int index, out string parameterName, out string replacement)
        {
            var node = this;
            parameterName = null;
            replacement = null;
            for (var cursor = index; cursor < sql.Length; cursor++)
            {
                if (node._children.TryGetValue(sql[cursor], out node) == false)
                    break;
                if (node._parameterName == null)
                    continue;
                parameterName = node._parameterName;
                replacement = node._replacement;
            }
            return parameterName != null;
        }
    }

    /// <summary>
    /// 判断 SQL 代码上下文中是否包含独立参数标记，并忽略字符串、注释和标识符中的文本。
    /// </summary>
    /// <param name="sql">待扫描的 SQL 文本。</param>
    /// <param name="parameterName">包含方言前缀的参数名称。</param>
    /// <returns>代码上下文包含该参数标记时返回 true。</returns>
    internal static bool ContainsParameterToken(string sql, string parameterName)
    {
        if (string.IsNullOrEmpty(sql) || string.IsNullOrWhiteSpace(parameterName))
            return false;

        var ignored = new StringBuilder();
        for (var index = 0; index < sql.Length;)
        {
            var current = sql[index];
            if (current == '\'')
            {
                index = AppendQuotedSegment(sql, ignored, index, '\'');
                continue;
            }
            if (current == '"')
            {
                index = AppendQuotedSegment(sql, ignored, index, '"');
                continue;
            }
            if (current == '`')
            {
                index = AppendQuotedSegment(sql, ignored, index, '`');
                continue;
            }
            if (current == '[')
            {
                index = AppendBracketedIdentifier(sql, ignored, index);
                continue;
            }
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = AppendLineComment(sql, ignored, index);
                continue;
            }
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = AppendBlockComment(sql, ignored, index);
                continue;
            }
            if (current == '$' && TryAppendDollarQuotedSegment(sql, ignored, ref index))
                continue;
            if (IsParameterToken(sql, index, parameterName))
                return true;
            index++;
        }
        return false;
    }

    /// <summary>
    /// 判断 SQL 代码上下文中是否包含独立关键字，并忽略字符串、注释和标识符中的文本。
    /// </summary>
    /// <param name="sql">待扫描的 SQL 文本。</param>
    /// <param name="keyword">不含分隔符的关键字。</param>
    /// <returns>代码上下文包含该关键字时返回 true。</returns>
    internal static bool ContainsSqlKeyword(string sql, string keyword)
    {
        if (string.IsNullOrEmpty(sql) || string.IsNullOrWhiteSpace(keyword))
            return false;

        var ignored = new StringBuilder();
        for (var index = 0; index < sql.Length;)
        {
            var current = sql[index];
            if (current == '\'')
            {
                index = AppendQuotedSegment(sql, ignored, index, '\'');
                continue;
            }
            if (current == '"')
            {
                index = AppendQuotedSegment(sql, ignored, index, '"');
                continue;
            }
            if (current == '`')
            {
                index = AppendQuotedSegment(sql, ignored, index, '`');
                continue;
            }
            if (current == '[')
            {
                index = AppendBracketedIdentifier(sql, ignored, index);
                continue;
            }
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = AppendLineComment(sql, ignored, index);
                continue;
            }
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = AppendBlockComment(sql, ignored, index);
                continue;
            }
            if (current == '$' && TryAppendDollarQuotedSegment(sql, ignored, ref index))
                continue;
            if (IsSqlKeywordToken(sql, index, keyword))
                return true;
            index++;
        }
        return false;
    }

    /// <summary>
    /// 追加单引号、双引号或反引号包裹的 SQL 片段，并处理连续引号转义。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">引号起始位置。</param>
    /// <param name="quote">当前引号字符。</param>
    /// <returns>下一个待处理字符的位置。</returns>
    private static int AppendQuotedSegment(string sql, StringBuilder result, int index, char quote)
    {
        result.Append(sql[index]);
        index++;
        while (index < sql.Length)
        {
            var current = sql[index++];
            result.Append(current);
            if (current != quote)
                continue;
            if (index >= sql.Length)
                break;
            if (sql[index] == quote)
            {
                result.Append(sql[index]);
                index++;
                continue;
            }
            break;
        }
        return index;
    }

    /// <summary>
    /// 追加方括号标识符片段，并处理连续右括号转义。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">方括号起始位置。</param>
    /// <returns>下一个待处理字符的位置。</returns>
    private static int AppendBracketedIdentifier(string sql, StringBuilder result, int index)
    {
        do
        {
            var current = sql[index];
            result.Append(current);
            index++;
            if (current != ']' || index >= sql.Length)
                continue;
            if (sql[index] == ']')
            {
                result.Append(sql[index]);
                index++;
                continue;
            }
            break;
        } while (index < sql.Length);
        return index;
    }

    /// <summary>
    /// 追加单行注释片段。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">注释起始位置。</param>
    /// <returns>下一个待处理字符的位置。</returns>
    private static int AppendLineComment(string sql, StringBuilder result, int index)
    {
        while (index < sql.Length)
        {
            var current = sql[index++];
            result.Append(current);
            if (current is '\r' or '\n')
                break;
        }
        return index;
    }

    /// <summary>
    /// 追加块注释片段。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">注释起始位置。</param>
    /// <returns>下一个待处理字符的位置。</returns>
    private static int AppendBlockComment(string sql, StringBuilder result, int index)
    {
        while (index < sql.Length)
        {
            var current = sql[index++];
            result.Append(current);
            if (current == '*' && index < sql.Length && sql[index] == '/')
            {
                result.Append(sql[index]);
                return index + 1;
            }
        }
        return index;
    }

    /// <summary>
    /// 尝试追加 PostgreSQL dollar-quoted 文本片段。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">当前字符位置。</param>
    /// <returns>找到并追加 dollar-quoted 片段时返回 true。</returns>
    private static bool TryAppendDollarQuotedSegment(string sql, StringBuilder result, ref int index)
    {
        var delimiterEnd = index + 1;
        while (delimiterEnd < sql.Length && (char.IsLetterOrDigit(sql[delimiterEnd]) || sql[delimiterEnd] == '_'))
            delimiterEnd++;
        if (delimiterEnd >= sql.Length || sql[delimiterEnd] != '$')
            return false;

        var delimiter = sql.Substring(index, delimiterEnd - index + 1);
        var contentEnd = sql.IndexOf(delimiter, delimiterEnd + 1, StringComparison.Ordinal);
        if (contentEnd < 0)
            return false;

        result.Append(sql, index, contentEnd + delimiter.Length - index);
        index = contentEnd + delimiter.Length;
        return true;
    }

    /// <summary>
    /// 判断当前位置是否为独立 SQL 参数标记。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="index">当前字符位置。</param>
    /// <param name="parameterName">包含方言前缀的参数名。</param>
    /// <returns>是独立参数标记时返回 true。</returns>
    private static bool IsParameterToken(string sql, int index, string parameterName)
    {
        if (index + parameterName.Length > sql.Length ||
            string.Compare(sql, index, parameterName, 0, parameterName.Length, StringComparison.Ordinal) != 0)
            return false;
        if (index > 0 && IsParameterNameCharacter(sql[index - 1]))
            return false;
        if (index + parameterName.Length < sql.Length && IsParameterNameCharacter(sql[index + parameterName.Length]))
            return false;
        if (parameterName[0] == '@' && index > 0 && sql[index - 1] == '@')
            return false;
        if (parameterName[0] == ':' &&
            ((index > 0 && sql[index - 1] == ':') ||
             (index + parameterName.Length < sql.Length && sql[index + parameterName.Length] == ':')))
            return false;
        return true;
    }

    /// <summary>
    /// 判断指定位置是否是独立 SQL 关键字。
    /// </summary>
    /// <param name="sql">待扫描 SQL。</param>
    /// <param name="index">关键字起始位置。</param>
    /// <param name="keyword">关键字。</param>
    /// <returns>当前位置是独立关键字时返回 true。</returns>
    private static bool IsSqlKeywordToken(string sql, int index, string keyword)
    {
        if (index + keyword.Length > sql.Length ||
            string.Compare(sql, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return false;
        if (index > 0 && IsParameterNameCharacter(sql[index - 1]))
            return false;
        return index + keyword.Length >= sql.Length ||
               IsParameterNameCharacter(sql[index + keyword.Length]) == false;
    }

    /// <summary>
    /// 判断字符是否属于 SQL 参数名称。
    /// </summary>
    /// <param name="value">待判断字符。</param>
    /// <returns>属于参数名称时返回 true。</returns>
    private static bool IsParameterNameCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

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
        var aliasRegister = new EntityAliasRegister();
        var originalAliasRegister = AliasRegister;
        ISelectClause selectClause;
        IFromClause fromClause;
        IJoinClause joinClause;
        IWhereClause whereClause;
        IGroupByClause groupByClause;
        IOrderByClause orderByClause;
        try
        {
            AliasRegister = aliasRegister;
            selectClause = CreateSelectClause();
            fromClause = CreateFromClause();
            joinClause = CreateJoinClause();
            whereClause = CreateWhereClause();
            groupByClause = CreateGroupByClause();
            orderByClause = CreateOrderByClause();
        }
        finally
        {
            AliasRegister = originalAliasRegister;
        }

        AliasRegister = aliasRegister;
        _selectClause = selectClause;
        _fromClause = fromClause;
        _joinClause = joinClause;
        _whereClause = whereClause;
        _groupByClause = groupByClause;
        _orderByClause = orderByClause;
        _isAddFilters = false;
        ClearSqlParams();
        ClearPageParams();
        ClearUnionBuilders();
        ClearCte();
        _subqueryParameterNames.Clear();
        _insertClause?.Clear();
        _insertColumnsClause?.Clear();
        _valuesClause?.Clear();
        _updateClause?.Clear();
        _updateFromClause?.Clear();
        _setClause?.Clear();
        _deleteClause?.Clear();
        _deleteUsingClause?.Clear();
        _mutationWhereClause?.Clear();
        _returningClause?.Clear();
        _isMutationDataBoundaryApplied = false;
        AllowAllRows = false;
        _operationState = SqlBuilderOperationState.None;
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
    /// 使用候选 Select 子句原子替换当前投影。
    /// </summary>
    /// <param name="configure">配置候选 Select 子句的操作。</param>
    /// <remarks>
    /// 候选子句配置失败时，当前投影保持不变。
    /// </remarks>
    internal void ReplaceSelect(Action<ISelectClause> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var distinct = _selectClause?.IsDistinct == true;
        var selectClause = CreateSelectClause();
        configure(selectClause);
        if (distinct)
            selectClause.Distinct();
        _selectClause = selectClause;
    }

    /// <summary>
    /// 清空From子句
    /// </summary>
    public ISqlBuilder ClearFrom()
    {
        var fromClause = CreateFromClause();
        _fromClause?.Clear();
        _fromClause = fromClause;
        return this;
    }

    /// <summary>
    /// 清空Join子句
    /// </summary>
    public ISqlBuilder ClearJoin()
    {
        var joinClause = CreateJoinClause();
        _joinClause?.Clear();
        _joinClause = joinClause;
        return this;
    }

    /// <summary>
    /// 清空Where子句
    /// </summary>
    public ISqlBuilder ClearWhere()
    {
        var whereClause = CreateWhereClause();
        _isAddFilters = false;
        _whereClause = whereClause;
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
        ClearParameters(OffsetParam, LimitParam);
        Pager = new Pager();
        OffsetParam = null;
        LimitParam = null;
        return this;
    }

    /// <summary>
    /// 清除指定名称的参数，并保留其他参数的值及 SQL 元数据。
    /// </summary>
    /// <param name="parameterNames">待清除的参数名称。</param>
    private void ClearParameters(params string[] parameterNames)
    {
        if (parameterNames == null)
            return;
        var names = new HashSet<string>(parameterNames.Where(name => string.IsNullOrWhiteSpace(name) == false),
            StringComparer.Ordinal);
        if (names.Count == 0)
            return;
        var parameterSnapshot = ParameterManager.Clone();
        ParameterManager.Clear();
        var sqlParameters = (parameterSnapshot as IAdvancedParameterManager)?.GetSqlParams();
        foreach (var parameter in parameterSnapshot.GetParams())
        {
            if (names.Contains(parameter.Key))
                continue;
            if (sqlParameters != null && sqlParameters.TryGetValue(parameter.Key, out var sqlParameter) &&
                ParameterManager is IAdvancedParameterManager advancedParameterManager)
            {
                advancedParameterManager.Add(CloneSqlParameter(sqlParameter, parameter.Key));
                continue;
            }
            ParameterManager.Add(parameter.Key, parameter.Value);
        }
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
    public virtual ISqlBuilder New()
    {
        var result = CreateBuilder(CreateEmptyParameterManager());
        result.InheritExecutionContext(this);
        return result;
    }

    /// <summary>
    /// 继承父 Builder 已冻结的执行上下文和查询能力。
    /// </summary>
    /// <remarks>
    /// 委托式子查询由 <see cref="New"/> 创建，必须与父查询使用相同的租户、数据源、映射配置和能力快照。
    /// </remarks>
    /// <param name="source">提供冻结执行上下文的父 Builder。</param>
    private void InheritExecutionContext(SqlBuilderBase source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        ExecutionContext = source.ExecutionContext;
        QueryCapabilities = source.QueryCapabilities;
        EntityResolver = new EntityResolver(EntityMappingResolver, DatabaseContextAccessor, MetadataOptions, Options,
            DatabaseContextResolver, EntityModelMetadataProvider, ExecutionContext.DatabaseContext);
        _mutationContext = CreateMutationContext();
    }

    #endregion

    #region ToSql(生成Sql)

    /// <summary>
    /// 生成Sql语句
    /// </summary>
    public virtual string ToSql()
    {
        if (ShouldRenderFromSnapshot())
            return CreateRenderSnapshot().Sql;
        return ToSqlDirectly();
    }

    /// <summary>
    /// 创建查询执行使用的渲染快照，确保 SQL 文本与参数来源来自同一个 Builder。
    /// </summary>
    /// <remarks>
    /// 仅动态过滤或写入数据边界需要独立副本，普通查询继续复用当前 Builder 的既有参数生命周期。
    /// </remarks>
    /// <returns>当前 SQL 和对应参数 Builder 的渲染快照。</returns>
    internal SqlBuilderRenderSnapshot CreateRenderSnapshot()
    {
        if (ShouldRenderFromSnapshot() == false)
            return new SqlBuilderRenderSnapshot(ToSql(), this);
        var snapshot = (SqlBuilderBase)Clone();
        return new SqlBuilderRenderSnapshot(snapshot.RenderSnapshot(), snapshot);
    }

    /// <summary>
    /// 创建用于派生执行计划的独立 Builder，并提前固化动态过滤和数据边界。
    /// </summary>
    /// <returns>已应用执行边界但尚未渲染 SQL 的独立 Builder。</returns>
    internal ISqlBuilder CreateExecutionBuilderSnapshot()
    {
        var snapshot = (SqlBuilderBase)Clone();
        if (snapshot.ShouldApplyFiltersOnRender())
            snapshot.EnsureFiltersAdded();
        if (snapshot.ShouldApplyMutationDataBoundaryOnRender())
            snapshot.EnsureMutationDataBoundary();
        return snapshot;
    }

    /// <summary>
    /// 判断当前语句是否需要从独立 Builder 快照渲染。
    /// </summary>
    /// <returns>需要使用独立快照时返回 true。</returns>
    private bool ShouldRenderFromSnapshot() =>
        ShouldApplyFiltersOnRender() || ShouldApplyMutationDataBoundaryOnRender();

    /// <summary>
    /// 直接从当前 Builder 生成 SQL，不应用独立快照数据边界。
    /// </summary>
    /// <returns>当前 Builder 生成的 SQL。</returns>
    private string ToSqlDirectly()
    {
        var result = new StringBuilder(256);
        AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 在同一独立快照中应用动态数据边界并生成 SQL。
    /// </summary>
    /// <returns>与当前快照参数集合严格对应的 SQL。</returns>
    private string RenderSnapshot()
    {
        if (ShouldApplyFiltersOnRender())
            EnsureFiltersAdded();
        if (ShouldApplyMutationDataBoundaryOnRender())
            EnsureMutationDataBoundary();
        var result = new StringBuilder(256);
        AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 从同一最终渲染快照创建写入命令，确保动态过滤生成的 SQL 与参数保持一致。
    /// </summary>
    /// <returns>包含当前写入 SQL 和参数的独立命令快照。</returns>
    internal SqlWriteCommand CreateWriteCommandSnapshot()
    {
        var snapshot = (SqlBuilderBase)Clone();
        if (snapshot.ShouldApplyFiltersOnRender())
            snapshot.EnsureFiltersAdded();
        if (snapshot.ShouldApplyMutationDataBoundaryOnRender())
            snapshot.EnsureMutationDataBoundary();
        var hasReturning = snapshot is IReturningClauseAccessor { ReturningClause.IsEmpty: false };
        return new SqlWriteCommand(snapshot.RenderSnapshot(), snapshot.GetSqlParams()?.Values, snapshot.Provider,
            snapshot.OperationKind, hasReturning);
    }

    /// <summary>
    /// 判断当前语句是否需要在独立渲染快照中应用全局过滤器。
    /// </summary>
    private bool ShouldApplyFiltersOnRender()
    {
        if (_isAddFilters || (OperationKind is not (SqlOperationKind.Select or SqlOperationKind.InsertSelect)))
            return false;
        var activeFilters = Services.Filters.ToList();
        if (activeFilters.Any(filter => filter is not IsDeletedFilter))
            return activeFilters.Count > 0;
        if (activeFilters.Any(filter => filter is IsDeletedFilter) == false)
            return false;
        return GetFilterSources().Any(source => source.EntityType != null &&
            typeof(ISoftDelete).IsAssignableFrom(source.EntityType));
    }

    /// <summary>
    /// 判断当前 Mutation 是否需要在独立渲染快照中追加默认数据边界。
    /// </summary>
    private bool ShouldApplyMutationDataBoundaryOnRender()
    {
        if (_isMutationDataBoundaryApplied)
            return false;
        return _operationState switch
        {
            SqlBuilderOperationState.Update => SqlMutationDataBoundary.ShouldApply(MutationContext, UpdateClause.Table,
                SqlDataBoundaryOperation.Update),
            SqlBuilderOperationState.Delete => SqlMutationDataBoundary.ShouldApply(MutationContext, DeleteClause.Table,
                SqlDataBoundaryOperation.Delete),
            _ => false
        };
    }

    /// <summary>
    /// 获取当前查询中可由默认过滤器处理的根表和 Join 表来源。
    /// </summary>
    private IEnumerable<TableSource> GetFilterSources()
    {
        if (FromClause is FromClause fromClause)
        {
            foreach (var source in fromClause.Sources)
                yield return source;
        }
        if (JoinClause is JoinClause joinClause)
        {
            foreach (var source in joinClause.GetTypedSources())
                yield return source;
        }
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
        ValidateQueryCapabilities();
        FromClause.Validate();
        OrderByClause.Validate(IsLimit);
    }

    /// <summary>
    /// 合并并冻结 Provider、数据源与选项的查询语法能力。
    /// </summary>
    /// <returns>当前 Builder 使用的查询语法能力。</returns>
    private SqlQueryCapabilities ResolveQueryCapabilities()
    {
        var provider = SqlProviderCapabilityResolver.GetQueryCapabilities(Provider);
        var dataSource = ExecutionContext.DatabaseContext?.DataSource?.QueryCapabilities;
        var options = Options?.QueryCapabilities;
        return new SqlQueryCapabilities
        {
            Cte = ResolveQueryCapability(provider.Cte, dataSource?.Cte,
                options?.Cte),
            Union = ResolveQueryCapability(provider.Union, dataSource?.Union,
                options?.Union),
            UnionAll = ResolveQueryCapability(provider.UnionAll,
                dataSource?.UnionAll, options?.UnionAll),
            Intersect = ResolveQueryCapability(provider.Intersect,
                dataSource?.Intersect, options?.Intersect),
            Except = ResolveQueryCapability(provider.Except,
                dataSource?.Except, options?.Except),
            RightJoin = ResolveQueryCapability(provider.RightJoin,
                dataSource?.RightJoin, options?.RightJoin),
            FullJoin = ResolveQueryCapability(provider.FullJoin,
                dataSource?.FullJoin, options?.FullJoin),
            Pagination = ResolveQueryCapability(provider.Pagination,
                dataSource?.Pagination, options?.Pagination)
        };
    }

    /// <summary>
    /// 解析单项查询语法能力。
    /// </summary>
    /// <param name="provider">Provider 能力基线。</param>
    /// <param name="dataSource">数据源能力覆盖。</param>
    /// <param name="options">选项能力覆盖。</param>
    /// <returns>最终能力状态。</returns>
    private static SqlQueryCapabilityState ResolveQueryCapability(SqlQueryCapabilityState provider,
        SqlQueryCapabilityState? dataSource, SqlQueryCapabilityState? options)
    {
        if (provider == SqlQueryCapabilityState.Unsupported)
            return SqlQueryCapabilityState.Unsupported;
        if (options is { } option && option != SqlQueryCapabilityState.Inherit)
            return option;
        if (dataSource is { } source && source != SqlQueryCapabilityState.Inherit)
            return source;
        return provider;
    }

    /// <summary>
    /// 验证当前查询使用的语法均已由冻结能力配置确认。
    /// </summary>
    private void ValidateQueryCapabilities()
    {
        if (CteItems.Count > 0)
            ValidateQueryCapability(QueryCapabilities.Cte, "CTE");
        foreach (var item in UnionItems)
            ValidateQueryCapability(item.Name);
        if (JoinClause is JoinClause joinClause && joinClause.ContainsJoinType("Right Join"))
            ValidateQueryCapability(QueryCapabilities.RightJoin, "Right Join");
        if (JoinClause is JoinClause fullJoinClause && fullJoinClause.ContainsJoinType("Full Join"))
            ValidateQueryCapability(QueryCapabilities.FullJoin, "Full Join");
        if (IsLimit)
            ValidateQueryCapability(QueryCapabilities.Pagination, "分页");
    }

    /// <summary>
    /// 在结构化 Lambda Join 提交前验证 Provider 的 Join 能力。
    /// </summary>
    /// <param name="joinType">待验证的 Join 类型。</param>
    internal void ValidateTypedJoinCapability(string joinType)
    {
        if (joinType == "Right Join")
            ValidateQueryCapability(QueryCapabilities.RightJoin, joinType);
        else if (joinType == "Full Join")
            ValidateQueryCapability(QueryCapabilities.FullJoin, joinType);
    }

    /// <summary>
    /// 验证集合操作语法能力。
    /// </summary>
    /// <param name="operation">集合操作关键字。</param>
    private void ValidateQueryCapability(string operation)
    {
        var capability = operation switch
        {
            "Union" => QueryCapabilities.Union,
            "Union All" => QueryCapabilities.UnionAll,
            "Intersect" => QueryCapabilities.Intersect,
            "Except" => QueryCapabilities.Except,
            "Right Join" => QueryCapabilities.RightJoin,
            "Full Join" => QueryCapabilities.FullJoin,
            _ => SqlQueryCapabilityState.Unsupported
        };
        ValidateQueryCapability(capability, operation);
    }

    /// <summary>
    /// 验证单项查询语法能力。
    /// </summary>
    /// <param name="capability">能力状态。</param>
    /// <param name="name">语法名称。</param>
    private void ValidateQueryCapability(SqlQueryCapabilityState capability, string name)
    {
        if (capability == SqlQueryCapabilityState.Supported)
            return;
        throw new NotSupportedException($"Provider {Provider.Key} 的当前查询能力配置不支持 {name}。");
    }

    /// <summary>
    /// 创建Sql语句
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateSql(StringBuilder result)
    {
        if (CteItems.Count == 0 && IsUnion == false)
        {
            CreateSqlByNoUnion(result);
            return;
        }
        var parameterSnapshot = ParameterManager.Clone();
        var parameterNameSnapshot = _subqueryParameterNames.ToDictionary(item => item.Key,
            item => new Dictionary<string, string>(item.Value), ReferenceComparer<ISqlBuilder>.Instance);
        var startIndex = result.Length;
        try
        {
            // 创建CTE
            CreateCte(result);
            if (IsUnion)
            {
                CreateSqlByUnion(result);
                return;
            }
            CreateSqlByNoUnion(result);
        }
        catch
        {
            RestoreQueryRenderSnapshot(parameterSnapshot, parameterNameSnapshot);
            result.Remove(startIndex, result.Length - startIndex);
            throw;
        }
    }

    /// <summary>
    /// 恢复查询渲染前的参数和子查询参数重命名状态。
    /// </summary>
    /// <param name="parameterSnapshot">渲染前的参数副本。</param>
    /// <param name="parameterNameSnapshot">渲染前的子查询参数重命名映射。</param>
    private void RestoreQueryRenderSnapshot(IParameterManager parameterSnapshot,
        IReadOnlyDictionary<ISqlBuilder, Dictionary<string, string>> parameterNameSnapshot)
    {
        ParameterManager.Clear();
        var sqlParameters = (parameterSnapshot as IAdvancedParameterManager)?.GetSqlParams();
        foreach (var parameter in parameterSnapshot.GetParams())
        {
            if (sqlParameters != null && sqlParameters.TryGetValue(parameter.Key, out var sqlParameter) &&
                ParameterManager is IAdvancedParameterManager advancedParameterManager)
            {
                advancedParameterManager.Add(CloneSqlParameter(sqlParameter, parameter.Key));
                continue;
            }
            ParameterManager.Add(parameter.Key, parameter.Value);
        }

        _subqueryParameterNames.Clear();
        foreach (var item in parameterNameSnapshot)
            _subqueryParameterNames[item.Key] = new Dictionary<string, string>(item.Value);
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
    protected virtual void CreateSqlByUnion(StringBuilder result)
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
        var context = new SqlFilterContext(Dialect, this, Services, ExecutionContext.DatabaseContext);
        foreach (var filter in Services.Filters)
            filter.Filter(context);
        ApplyFilterPlacements(SqlFilterPlacementPlanner.Plan(context));
    }

    /// <summary>
    /// 将完整 Join 拓扑已规划的过滤器谓词提交到最终 Where 或指定 Join On。
    /// </summary>
    /// <param name="placements">按过滤器贡献顺序排列的谓词放置决定。</param>
    private void ApplyFilterPlacements(IEnumerable<SqlFilterPlacement> placements)
    {
        if (placements == null)
            return;
        foreach (var placement in placements)
        {
            var predicate = placement.Predicate;
            if (string.IsNullOrWhiteSpace(placement.JoinSourceId))
            {
                WhereClause.Where(predicate.Column, predicate.Value, predicate.Operator);
                continue;
            }
            if (JoinClause is not JoinClause joinClause)
                throw new InvalidOperationException($"未找到过滤器来源 {placement.JoinSourceId} 对应的 Join 子句。");
            joinClause.AddFilterCondition(placement.JoinSourceId, predicate.Column, predicate.Value,
                predicate.Operator);
        }
    }

    /// <summary>
    /// 添加分页Sql
    /// </summary>
    /// <param name="result">Sql拼接器</param>
    protected void AppendLimit(StringBuilder result)
    {
        if (IsLimit)
            AppendSql(result, CreateLimitSql());
    }

    /// <summary>
    /// 创建分页Sql
    /// </summary>
    protected virtual string CreateLimitSql()
    {
        if (string.IsNullOrWhiteSpace(OffsetParam) == false)
            return Provider.PaginationRenderer.Render(OffsetParam, GetLimitParam());
        var offsetParameter = ParameterManager.Clone().GenerateName();
        var sql = Provider.PaginationRenderer.Render(offsetParameter, GetLimitParam());
        ParameterManager.Add(offsetParameter, 0);
        OffsetParam = offsetParameter;
        return sql;
    }

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
        ValidateOperation(SqlOperationAction.Paging);
        if (string.IsNullOrWhiteSpace(OffsetParam))
        {
            var parameterManager = ParameterManager;
            var parameter = parameterManager.GenerateName();
            var parameterProbe = parameterManager.Clone();
            parameterProbe.Add(parameter, 0);
            parameterProbe.Add(parameter, count);
            parameterManager.Add(parameter, 0);
            parameterManager.Add(parameter, count);
            OffsetParam = parameter;
            UseOperation(SqlOperationAction.Paging);
            return this;
        }
        ParameterManager.Add(OffsetParam, count);
        UseOperation(SqlOperationAction.Paging);
        return this;
    }

    /// <summary>
    /// 获取跳过行数的参数名
    /// </summary>
    protected string GetOffsetParam()
    {
        if (string.IsNullOrWhiteSpace(OffsetParam) == false)
            return OffsetParam;
        var parameter = ParameterManager.GenerateName();
        ParameterManager.Add(parameter, 0);
        OffsetParam = parameter;
        return parameter;
    }

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="count">获取的行数</param>
    public ISqlBuilder Take(int count)
    {
        ValidateOperation(SqlOperationAction.Paging);
        if (string.IsNullOrWhiteSpace(LimitParam))
        {
            var parameterManager = ParameterManager;
            var parameter = parameterManager.GenerateName();
            var parameterProbe = parameterManager.Clone();
            parameterProbe.Add(parameter, count);
            parameterManager.Add(parameter, count);
            LimitParam = parameter;
        }
        else
            ParameterManager.Add(LimitParam, count);
        Pager.PageSize = count;
        UseOperation(SqlOperationAction.Paging);
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
        ValidateOperation(SqlOperationAction.Paging);
        var parameterManager = ParameterManager;
        var parameterProbe = parameterManager.Clone();
        var offsetParameter = OffsetParam;
        var limitParameter = LimitParam;
        if (string.IsNullOrWhiteSpace(offsetParameter))
        {
            offsetParameter = parameterProbe.GenerateName();
            parameterProbe.Add(offsetParameter, 0);
        }
        parameterProbe.Add(offsetParameter, pager.GetSkipCount());
        if (string.IsNullOrWhiteSpace(limitParameter))
            limitParameter = parameterProbe.GenerateName();
        parameterProbe.Add(limitParameter, pager.PageSize);

        if (string.IsNullOrWhiteSpace(OffsetParam))
        {
            OffsetParam = parameterManager.GenerateName();
            parameterManager.Add(OffsetParam, 0);
        }
        parameterManager.Add(OffsetParam, pager.GetSkipCount());
        if (string.IsNullOrWhiteSpace(LimitParam))
            LimitParam = parameterManager.GenerateName();
        parameterManager.Add(LimitParam, pager.PageSize);
        Pager = pager;
        UseOperation(SqlOperationAction.Paging);
        return this;
    }

    #endregion

    /// <inheritdoc />
    public void SetAllowAllRows(bool allowAllRows)
    {
        UseOperation(SqlOperationAction.AllowAllRows);
        AllowAllRows = allowAllRows;
    }

    /// <inheritdoc />
    void ISqlOperationStateManager.UseOperation(SqlOperationAction action) => UseOperation(action);

    /// <inheritdoc />
    void ISqlOperationStateManager.ValidateOperation(SqlOperationAction action) => ValidateOperation(action);

    /// <summary>
    /// 在 Clause 修改前验证并切换当前操作状态。
    /// </summary>
    /// <summary>
    /// 验证 Fluent 操作可否从当前状态迁移，并在合法时更新状态。
    /// </summary>
    /// <param name="action">即将执行的查询或 Mutation 操作。</param>
    /// <exception cref="InvalidOperationException">操作会混用不兼容的查询和 Mutation 语句时抛出。</exception>
    private void UseOperation(SqlOperationAction action)
    {
        _operationState = GetNextOperationStateOrThrow(action);
    }

    /// <summary>
    /// 验证操作状态转换，但不改变当前状态。
    /// </summary>
    /// <param name="action">即将执行的查询或 Mutation 操作。</param>
    private void ValidateOperation(SqlOperationAction action) => GetNextOperationStateOrThrow(action);

    /// <summary>
    /// 获取合法操作状态转换的目标状态，或抛出统一异常。
    /// </summary>
    /// <param name="action">即将执行的查询或 Mutation 操作。</param>
    /// <returns>状态转换后的目标状态。</returns>
    private SqlBuilderOperationState GetNextOperationStateOrThrow(SqlOperationAction action)
    {
        var nextState = GetNextOperationState(action);
        if (nextState.HasValue)
            return nextState.Value;
        throw new InvalidOperationException($"当前 Builder 已处于 {GetOperationName(_operationState)} 状态，不能调用 {GetActionName(action)}。");
    }

    /// <summary>
    /// 获取当前状态执行指定操作后的目标状态。
    /// </summary>
    /// <param name="action">即将执行的操作。</param>
    /// <returns>合法迁移后的状态；无法迁移时返回 <see langword="null"/>。</returns>
    private SqlBuilderOperationState? GetNextOperationState(SqlOperationAction action)
    {
        return _operationState switch
        {
            SqlBuilderOperationState.None => action switch
            {
                SqlOperationAction.Select or SqlOperationAction.QueryClause or SqlOperationAction.Paging =>
                    SqlBuilderOperationState.Select,
                SqlOperationAction.InsertInto => SqlBuilderOperationState.InsertPending,
                SqlOperationAction.Values => SqlBuilderOperationState.InsertValues,
                SqlOperationAction.Update or SqlOperationAction.Set => SqlBuilderOperationState.Update,
                SqlOperationAction.DeleteFrom => SqlBuilderOperationState.Delete,
                _ => null
            },
            SqlBuilderOperationState.Select when action is SqlOperationAction.Select or
                SqlOperationAction.QueryClause or SqlOperationAction.Paging => SqlBuilderOperationState.Select,
            SqlBuilderOperationState.InsertPending => action switch
            {
                SqlOperationAction.InsertInto => SqlBuilderOperationState.InsertPending,
                SqlOperationAction.Values => SqlBuilderOperationState.InsertValues,
                SqlOperationAction.Select or SqlOperationAction.QueryClause =>
                    SqlBuilderOperationState.InsertSelect,
                _ => null
            },
            SqlBuilderOperationState.InsertValues when action is SqlOperationAction.InsertInto or
                SqlOperationAction.Values or SqlOperationAction.Returning => SqlBuilderOperationState.InsertValues,
            SqlBuilderOperationState.InsertSelect when action is SqlOperationAction.InsertInto or
                SqlOperationAction.Select or SqlOperationAction.QueryClause or SqlOperationAction.Returning =>
                SqlBuilderOperationState.InsertSelect,
            SqlBuilderOperationState.Update when action is SqlOperationAction.Update or SqlOperationAction.UpdateFrom or
                SqlOperationAction.Set or
                SqlOperationAction.MutationWhere or SqlOperationAction.Returning or
                SqlOperationAction.AllowAllRows => SqlBuilderOperationState.Update,
            SqlBuilderOperationState.Delete when action is SqlOperationAction.DeleteFrom or SqlOperationAction.DeleteUsing or
                SqlOperationAction.MutationWhere or SqlOperationAction.Returning or
                SqlOperationAction.AllowAllRows => SqlBuilderOperationState.Delete,
            _ => null
        };
    }

    /// <summary>
    /// 获取用于异常消息的操作状态名称。
    /// </summary>
    /// <param name="state">当前 Builder 操作状态。</param>
    /// <returns>面向诊断的状态名称。</returns>
    private static string GetOperationName(SqlBuilderOperationState state) => state switch
    {
        SqlBuilderOperationState.InsertPending => "Insert",
        SqlBuilderOperationState.InsertValues => "InsertValues",
        SqlBuilderOperationState.InsertSelect => "InsertSelect",
        SqlBuilderOperationState.Select => "Select",
        SqlBuilderOperationState.Update => "Update",
        SqlBuilderOperationState.Delete => "Delete",
        _ => "None"
    };

    /// <summary>
    /// 获取用于异常消息的 Fluent 操作名称。
    /// </summary>
    /// <param name="action">当前请求的 Fluent 操作。</param>
    /// <returns>面向诊断的操作名称。</returns>
    private static string GetActionName(SqlOperationAction action) => action switch
    {
        SqlOperationAction.InsertInto => "InsertInto",
        SqlOperationAction.Values => "Values",
        SqlOperationAction.Update => "Update",
        SqlOperationAction.UpdateFrom => "UpdateFrom",
        SqlOperationAction.Set => "Set",
        SqlOperationAction.DeleteFrom => "DeleteFrom",
        SqlOperationAction.DeleteUsing => "DeleteUsing",
        SqlOperationAction.MutationWhere or SqlOperationAction.QueryClause => "Where",
        SqlOperationAction.Returning => "Returning",
        SqlOperationAction.AllowAllRows => "AllowAllRows",
        SqlOperationAction.Paging => "Paging",
        _ => "Select"
    };

    private IMutationWhereClause MutationWhereClause =>
        ((IMutationWhereClauseAccessor)this).WhereClause;

    /// <summary>
    /// 验证并按 Insert Values 语法顺序渲染目标表、列、可选返回投影和数据行。
    /// </summary>
    /// <param name="builder">用于追加 SQL 的字符串生成器。</param>
    private void AppendInsertValues(StringBuilder builder)
    {
        var validationContext = new SqlValidationContext(Provider, ParameterManager.Count, false,
            SqlExecutionKind.Insert);
        InsertClause.Validate(validationContext);
        InsertColumnsClause.Validate(validationContext);
        ValuesClause.Validate(validationContext);
        if (InsertColumnsClause.Columns.Count != ValuesClause.ColumnCount)
            throw new InvalidOperationException("Insert 列数量与 Values 列数量不一致。");
        InsertClause.AppendTo(builder);
        InsertColumnsClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.BeforeSource);
        ValuesClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.End);
    }

    /// <summary>
    /// 验证并渲染 Update、Set、可选 Update From、筛选与 Returning 子句。
    /// </summary>
    /// <param name="builder">用于追加 SQL 的字符串生成器。</param>
    private void AppendUpdate(StringBuilder builder)
    {
        var validationContext = new SqlValidationContext(Provider, ParameterManager.Count, AllowAllRows,
            SqlExecutionKind.Update);
        UpdateClause.Validate(validationContext);
        SetClause.Validate(validationContext);
        if (UpdateFromClause.Table != null)
            UpdateFromClause.Validate(validationContext);
        MutationWhereClause.Validate(validationContext);
        UpdateClause.AppendTo(builder);
        SetClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.BeforeSource);
        if (UpdateFromClause.Table != null)
            UpdateFromClause.AppendTo(builder);
        MutationWhereClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.End);
    }

    /// <summary>
    /// 验证并渲染 Insert Select；该模式不允许同时使用 Values、Union 或 CTE。
    /// </summary>
    /// <param name="builder">用于追加 SQL 的字符串生成器。</param>
    private void AppendInsertSelect(StringBuilder builder)
    {
        if (ValuesClause.RowCount > 0)
            throw new InvalidOperationException("Insert Select 不能同时包含 Values。");
        if (UnionItems.Count > 0 || CteItems.Count > 0)
            throw new NotSupportedException("Insert Select 当前不支持 Union 或 CTE。");
        var validationContext = new SqlValidationContext(Provider, ParameterManager.Count, false,
            SqlExecutionKind.Insert);
        InsertClause.Validate(validationContext);
        FromClause.Validate();
        OrderByClause.Validate(false);
        var targetColumnCount = InsertColumnsClause.Columns.Count;
        if (targetColumnCount > 0 && SelectClause.ProjectionCount is int projectionCount &&
            targetColumnCount != projectionCount)
            throw new InvalidOperationException("Insert Select 的目标列数量与查询输出列数量不一致。");
        InsertClause.AppendTo(builder);
        if (targetColumnCount > 0)
            InsertColumnsClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.BeforeSource);
        builder.AppendLine(" ");
        AppendSelect(builder);
        AppendFrom(builder);
        AppendClause(builder, JoinClause);
        AppendClause(builder, WhereClause);
        AppendClause(builder, GroupByClause);
        AppendClause(builder, OrderByClause);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.End);
    }

    /// <summary>
    /// 验证并渲染 Delete、可选 Using、筛选与 Returning 子句。
    /// </summary>
    /// <param name="builder">用于追加 SQL 的字符串生成器。</param>
    private void AppendDelete(StringBuilder builder)
    {
        var validationContext = new SqlValidationContext(Provider, ParameterManager.Count, AllowAllRows,
            SqlExecutionKind.Delete);
        DeleteClause.Validate(validationContext);
        if (DeleteUsingClause.Table != null)
            DeleteUsingClause.Validate(validationContext);
        MutationWhereClause.Validate(validationContext);
        DeleteClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.BeforeSource);
        if (DeleteUsingClause.Table != null)
            DeleteUsingClause.AppendTo(builder);
        MutationWhereClause.AppendTo(builder);
        AppendReturning(builder, validationContext, SqlReturningClausePosition.End);
    }

    /// <summary>
    /// 在首次 Mutation 渲染前将结构化实体目标的默认数据边界写入 Where 子句。
    /// </summary>
    private void EnsureMutationDataBoundary()
    {
        if (_isMutationDataBoundaryApplied)
            return;
        var table = _operationState == SqlBuilderOperationState.Update
            ? UpdateClause.Table
            : _operationState == SqlBuilderOperationState.Delete
                ? DeleteClause.Table
                : null;
        var operation = _operationState == SqlBuilderOperationState.Delete
            ? SqlDataBoundaryOperation.Delete
            : SqlDataBoundaryOperation.Update;
        _isMutationDataBoundaryApplied = SqlMutationDataBoundary.Apply(MutationContext, table, operation,
            MutationWhereClause);
    }

    /// <summary>
    /// 在当前 Provider 要求的位置渲染 Returning 或 SQL Server Output 投影。
    /// </summary>
    /// <param name="builder">用于追加 SQL 的字符串生成器。</param>
    /// <param name="validationContext">当前 Mutation 验证上下文。</param>
    /// <param name="position">正在渲染的 SQL 语法位置。</param>
    private void AppendReturning(StringBuilder builder, SqlValidationContext validationContext,
        SqlReturningClausePosition position)
    {
        if (ReturningClause.IsEmpty)
            return;
        var configuredPosition = GetReturningPosition();
        if (configuredPosition != position)
            return;
        ReturningClause.Validate(validationContext);
        TrimAppendedWhitespace(builder, 0);
        ReturningClause.AppendTo(builder);
    }

    /// <summary>
    /// 获取当前 Provider 的返回投影语法位置。
    /// </summary>
    /// <returns>语句末尾的 Returning 位置，或数据源前的 SQL Server Output 位置。</returns>
    private SqlReturningClausePosition GetReturningPosition()
    {
        var position = (Provider as ISqlReturningDialect)?.Position ?? SqlReturningClausePosition.End;
        if (position is SqlReturningClausePosition.End or SqlReturningClausePosition.BeforeSource)
            return position;
        throw new InvalidOperationException($"Provider {Provider.Key} 返回了无效的 Returning 子句位置。");
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        builder.CheckNull(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            if (ShouldRenderFromSnapshot())
            {
                var snapshot = (SqlBuilderBase)Clone();
                if (snapshot.ShouldApplyFiltersOnRender())
                    snapshot.EnsureFiltersAdded();
                if (snapshot.ShouldApplyMutationDataBoundaryOnRender())
                    snapshot.EnsureMutationDataBoundary();
                snapshot.AppendToCore(builder);
                return;
            }
            AppendToCore(builder);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <summary>
    /// 将当前 Builder 的 SQL 追加到缓冲区。
    /// </summary>
    private void AppendToCore(StringBuilder builder)
    {
        var startIndex = builder.Length;
        switch (_operationState)
        {
            case SqlBuilderOperationState.InsertValues:
                AppendInsertValues(builder);
                break;
            case SqlBuilderOperationState.Update:
                AppendUpdate(builder);
                break;
            case SqlBuilderOperationState.Delete:
                AppendDelete(builder);
                break;
            case SqlBuilderOperationState.InsertPending:
                throw new InvalidOperationException("InsertInto 后必须调用 Values 或 Select。");
            case SqlBuilderOperationState.InsertSelect:
                AppendInsertSelect(builder);
                break;
            default:
                AppendQuery(builder);
                break;
        }
        TrimAppendedWhitespace(builder, startIndex);
    }

    /// <summary>
    /// 渲染查询 SQL，并在初始化或验证失败时恢复分页临时追加前的排序状态。
    /// </summary>
    private void AppendQuery(StringBuilder builder)
    {
        var orderBySnapshot = _orderByClause?.Clone(CreateClauseContext());
        try
        {
            Init();
            Validate();
            CreateSql(builder);
        }
        catch
        {
            _orderByClause = orderBySnapshot;
            throw;
        }
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
/// SQL Builder 单次渲染的 SQL 与参数来源快照。
/// </summary>
internal sealed class SqlBuilderRenderSnapshot
{
    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderRenderSnapshot"/> 类型的实例。
    /// </summary>
    /// <param name="sql">渲染后的 SQL。</param>
    /// <param name="builder">与 SQL 对应的参数来源 Builder。</param>
    public SqlBuilderRenderSnapshot(string sql, ISqlBuilder builder)
    {
        Sql = sql;
        Builder = builder;
    }

    /// <summary>
    /// 渲染后的 SQL。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 与 SQL 对应的参数来源 Builder。
    /// </summary>
    public ISqlBuilder Builder { get; }
}

/// <summary>
/// 统一 SQL Builder 的查询与 Mutation 构造状态。
/// </summary>
internal enum SqlBuilderOperationState
{
    /// <summary>
    /// 尚未选择查询或 Mutation 操作。
    /// </summary>
    None,

    /// <summary>
    /// 正在构造普通 Select 查询。
    /// </summary>
    Select,

    /// <summary>
    /// 已设置 Insert 目标表，尚待 Values 或 Select 数据源。
    /// </summary>
    InsertPending,

    /// <summary>
    /// 正在构造 Insert Values 语句。
    /// </summary>
    InsertValues,

    /// <summary>
    /// 正在构造 Insert Select 语句。
    /// </summary>
    InsertSelect,

    /// <summary>
    /// 正在构造 Update 语句。
    /// </summary>
    Update,

    /// <summary>
    /// 正在构造 Delete 语句。
    /// </summary>
    Delete
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

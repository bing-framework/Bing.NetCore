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
    ICteAccessor, ISqlOperationStateManager
{

    /// <summary>
    /// 获取是否已配置会输出的行限制。
    /// </summary>
    internal bool HasLimit => IsLimit;

    /// <summary>
    /// 获取是否需要通过独立渲染快照应用动态数据边界。
    /// </summary>
    internal bool RequiresRenderSnapshot => ShouldRenderFromSnapshot();
    #region 字段

    /// <summary>仅独立渲染快照在当前调用期间拥有其已深克隆的 CTE/Union 子树。</summary>
    private bool _renderOwnsNestedBuilders;

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

    /// <summary>是否已添加查询内容。</summary>
    private bool _hasQueryContent;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private IParamLiteralsResolver _paramLiteralsResolver;

    /// <summary>
    /// 是否已添加过滤器
    /// </summary>
    private SqlQueryRenderState _executionRenderState;

    /// <summary>仅在 AppendToCore 调用期间存在，失败时直接丢弃。</summary>
    private SqlQueryRenderState _activeRenderState;

    /// <summary>仅在创建执行快照期间接收渲染边界，调用结束即恢复，不参与 Builder 克隆。</summary>
    private SqlBuilderRenderSnapshot _renderCapture;

    /// <summary>
    /// 当前查询引用的类型化子查询父上下文。
    /// </summary>
    private string _parentQueryContextId;

    /// <summary>
    /// 子查询参数重命名映射，确保重复渲染使用同一参数名称。
    /// </summary>
    private readonly Dictionary<object, Dictionary<string, string>> _draftParameterNames =
        new(ReferenceComparer<object>.Instance);

    private Dictionary<object, Dictionary<string, string>> SubqueryParameterNames =>
        _activeRenderState?.SubqueryParameterNames ?? _draftParameterNames;

    // 普通查询不分配逻辑键映射；渲染阶段的修改只在成功后发布。
    private Dictionary<string, SqlFragmentParameterBinding> _draftParameterKeys;

    private Dictionary<string, SqlFragmentParameterBinding> FragmentParameterKeys
    {
        get => _activeRenderState == null ? _draftParameterKeys : _activeRenderState.ParameterKeys;
        set
        {
            if (_activeRenderState == null)
                _draftParameterKeys = value;
            else
                _activeRenderState.ParameterKeys = value;
        }
    }

    #endregion

    #region 属性

    /// <summary>
    /// 获取或设置实体映射解析器
    /// </summary>
    protected IEntityMappingResolver EntityMappingResolver { get; private set; }

    /// <inheritdoc />
    public ISqlProvider Provider { get; }

    /// <summary>
    /// 获取或设置SQL Builder 可共享服务。
    /// </summary>
    protected SqlBuilderServices Services { get; private set; }

    /// <summary>
    /// 获取或设置实体模型原始元数据提供器。
    /// </summary>
    protected IEntityModelMetadataProvider EntityModelMetadataProvider { get; private set; }

    /// <summary>
    /// 获取或设置SQL 对象名称格式化器。
    /// </summary>
    protected ISqlObjectNameFormatter ObjectNameFormatter { get; private set; }

    /// <summary>
    /// 获取或设置跨数据库查询校验器。
    /// </summary>
    protected ISqlCrossDatabaseQueryValidator CrossDatabaseQueryValidator { get; private set; }

    /// <summary>
    /// 获取或设置SQL 表引用验证器。
    /// </summary>
    protected ISqlTableReferenceValidator TableReferenceValidator { get; private set; }

    /// <summary>
    /// 获取或设置数据库上下文访问器
    /// </summary>
    protected IDatabaseContextAccessor DatabaseContextAccessor { get; private set; }

    /// <summary>
    /// 获取或设置Sql 参数工厂
    /// </summary>
    protected ISqlParameterFactory SqlParameterFactory { get; private set; }

    /// <summary>
    /// 获取或设置Sql 元数据配置
    /// </summary>
    protected SqlMetadataOptions MetadataOptions { get; private set; }

    /// <summary>
    /// 获取或设置Sql 配置
    /// </summary>
    protected SqlOptions Options { get; private set; }

    /// <summary>
    /// 获取当前查询固定使用的自动别名策略。
    /// </summary>
    /// <remarks>查询克隆及子查询继承此值。</remarks>
    internal bool AutoGenerateTableAlias { get; private set; }

    /// <summary>
    /// 获取当前 Builder 对应的不可变别名策略。
    /// </summary>
    /// <remarks>保留 <see cref="AutoGenerateTableAlias"/> 作为兼容开关，内部计划使用策略值。</remarks>
    internal SqlAliasPolicy AliasPolicy => AutoGenerateTableAlias
        ? SqlAliasPolicy.Automatic
        : SqlAliasPolicy.ExplicitOnly;

    /// <summary>
    /// 获取或设置SQL 数据库上下文解析器
    /// </summary>
    protected ISqlDatabaseContextResolver DatabaseContextResolver { get; private set; }

    /// <summary>
    /// 获取或设置Builder 生命周期内固定的执行上下文。
    /// </summary>
    internal SqlBuilderExecutionContext ExecutionContext { get; private set; }

    /// <summary>
    /// 获取或设置实体解析器
    /// </summary>
    protected IEntityResolver EntityResolver { get; private set; }

    /// <summary>
    /// 获取或设置实体别名注册器
    /// </summary>
    protected IEntityAliasRegister AliasRegister { get; private set; }

    /// <summary>
    /// 获取当前执行上下文解析后的数据库类型。
    /// </summary>
    protected DatabaseType ExecutionProviderDatabaseType => ExecutionContext.DatabaseType ?? Provider.DatabaseType;

    /// <inheritdoc />
    public IParameterManager ParameterManager
    {
        get
        {
            if (_activeRenderState != null)
                return _activeRenderState.Parameters;
            if (_parameterManager == null)
            {
                _parameterManager = CreateParameterManager();
                _parameterManagerState.Current = _parameterManager;
            }
            return _parameterManager;
        }
    }

    /// <inheritdoc />
    public IDialect Dialect => Provider.Dialect;

    /// <inheritdoc />
    public ISelectClause SelectClause => _selectClause = _mutationCheckpoint != null &&
        ReferenceEquals(_selectClause, _mutationCheckpoint.Select) && _selectClause != null
        ? _selectClause.Clone(CreateClauseContext()) : _selectClause ?? CreateSelectClause();

    /// <inheritdoc />
    public IFromClause FromClause => _activeRenderState?.From != null
        ? _activeRenderState.From : _fromClause = _mutationCheckpoint != null &&
        ReferenceEquals(_fromClause, _mutationCheckpoint.From) && _fromClause != null
        ? _fromClause.Clone(CreateClauseContext()) : _fromClause ?? CreateFromClause();

    /// <inheritdoc />
    public IJoinClause JoinClause => _activeRenderState?.IsolatedFilters == true
        ? _activeRenderState.Join ??= _joinClause?.Clone(CreateClauseContext()) ?? CreateJoinClause()
        : _joinClause = _mutationCheckpoint != null &&
        ReferenceEquals(_joinClause, _mutationCheckpoint.Join) && _joinClause != null
        ? _joinClause.Clone(CreateClauseContext()) : _joinClause ?? CreateJoinClause();

    /// <inheritdoc />
    public IWhereClause WhereClause => _activeRenderState?.IsolatedFilters == true
        ? _activeRenderState.Where ??= _whereClause?.Clone(CreateClauseContext()) ?? CreateWhereClause()
        : _whereClause = _mutationCheckpoint != null &&
        ReferenceEquals(_whereClause, _mutationCheckpoint.Where) && _whereClause != null
        ? _whereClause.Clone(CreateClauseContext()) : _whereClause ?? CreateWhereClause();

    /// <inheritdoc />
    public IGroupByClause GroupByClause => _groupByClause = _mutationCheckpoint != null &&
        ReferenceEquals(_groupByClause, _mutationCheckpoint.GroupBy) && _groupByClause != null
        ? _groupByClause.Clone(CreateClauseContext()) : _groupByClause ?? CreateGroupByClause();

    /// <inheritdoc />
    public IOrderByClause OrderByClause => _activeRenderState != null
        ? _activeRenderState.OrderBy ??= CreateOrderByClause()
        : _orderByClause = _mutationCheckpoint != null &&
        ReferenceEquals(_orderByClause, _mutationCheckpoint.OrderBy) && _orderByClause != null
        ? _orderByClause.Clone(CreateClauseContext()) : _orderByClause ?? CreateOrderByClause();

    /// <inheritdoc />
    public SqlOperationKind OperationKind => _hasQueryContent ? SqlOperationKind.Select : SqlOperationKind.None;

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
        if (operationKind is not (SqlOperationKind.None or SqlOperationKind.Select))
            throw new ArgumentOutOfRangeException(nameof(operationKind));
        _hasQueryContent = operationKind == SqlOperationKind.Select;
    }

    /// <summary>
    /// 获取参数字面值解析器
    /// </summary>
    protected IParamLiteralsResolver ParamLiteralsResolver => _paramLiteralsResolver ??= GetParamLiteralsResolver();

    /// <summary>
    /// 获取或设置跳过行数参数名
    /// </summary>
    protected string OffsetParam
    {
        get => _activeRenderState == null ? _offsetParam : _activeRenderState.OffsetParam;
        private set { if (_activeRenderState == null) _offsetParam = value; else _activeRenderState.OffsetParam = value; }
    }
    private string _offsetParam;

    /// <summary>
    /// 获取或设置限制行数参数名
    /// </summary>
    protected string LimitParam
    {
        get => _activeRenderState == null ? _limitParam : _activeRenderState.LimitParam;
        private set { if (_activeRenderState == null) _limitParam = value; else _activeRenderState.LimitParam = value; }
    }
    private string _limitParam;

    /// <inheritdoc />
    public IPager Pager { get; private set; }

    /// <summary>
    /// 获取是否分组
    /// </summary>
    public bool IsGroup => GroupByClause.IsGroup;

    /// <summary>
    /// 获取是否限制行数
    /// </summary>
    protected bool IsLimit => string.IsNullOrWhiteSpace(LimitParam) == false;

    /// <inheritdoc />
    public bool IsUnion => _unionItems.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<BuilderItem> UnionItems => _unionItemsView ??= _unionItems.AsReadOnly();

    private readonly List<BuilderItem> _unionItems = new();
    private IReadOnlyList<BuilderItem> _unionItemsView;

    /// <inheritdoc />
    public IReadOnlyList<BuilderItem> CteItems => _cteItemsView ??= _cteItems.AsReadOnly();

    private readonly List<BuilderItem> _cteItems = new();
    private IReadOnlyList<BuilderItem> _cteItemsView;

    /// <inheritdoc />
    public void AddCte(BuilderItem item) => _cteItems.Add(item ?? throw new ArgumentNullException(nameof(item)));

    /// <inheritdoc />
    public void AddUnion(BuilderItem item) => _unionItems.Add(item ?? throw new ArgumentNullException(nameof(item)));

    /// <summary>
    /// 获取或设置Builder 创建时冻结的查询语法能力。
    /// </summary>
    private SqlQueryCapabilities QueryCapabilities { get; set; }

    /// <summary>仅已确认分页能力允许冻结 IR 追加分页；其余情况沿用 Builder 能力诊断。</summary>
    internal ISqlPaginationRenderer FrozenPaginationRenderer =>
        QueryCapabilities.Pagination == SqlQueryCapabilityState.Supported ? Provider.PaginationRenderer : null;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderBase"/> 类型的实例。
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
        AutoGenerateTableAlias = Services.Options?.AutoGenerateTableAlias ?? true;
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
    }

    #endregion

    #region 工厂方法

    /// <summary>
    /// 创建参数管理器
    /// </summary>
    /// <returns>按当前 SQL Provider 创建并应用参数数量限制的参数管理器。</returns>
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
        var maxParameterCount = SqlProviderCapabilityResolver.GetProfile(Provider).Limits?.MaxParameterCount;
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
    /// 获取内部查询使用的现有映射上下文。
    /// </summary>
    internal SqlClauseContext QueryClauseContext => CreateClauseContext();

    /// <summary>完整生成链只缓存内置、单来源、尚未配置其他子句的草稿。</summary>
    internal bool CanCacheGeneratedTemplate(bool replacesDefaultProjection) => SupportsMutationCheckpoint && ParameterManager is IAdvancedParameterManager &&
        UsesFrameworkParameterManager(ParameterManager) &&
        (_selectClause == null || replacesDefaultProjection && _selectClause is SelectClause
            { ProjectionMode: SqlProjectionMode.DefaultRoot, IsDistinct: false, HasAggregate: false } select &&
            (select.GetType().Assembly == typeof(SelectClause).Assembly || select.GetType().Assembly == GetType().Assembly)) &&
        _joinClause == null && _whereClause == null && _groupByClause == null && _orderByClause == null &&
        _fromClause is FromClause from && (from.GetType().Assembly == typeof(FromClause).Assembly || from.GetType().Assembly == GetType().Assembly) &&
        from.Sources.Count == 1 && from.Sources[0].Reference != null && from.Sources[0].EntityType != null &&
        ParameterManager.GetParams().Count == 0 && _unionItems.Count == 0 && _cteItems.Count == 0 &&
        OffsetParam == null && LimitParam == null && !RequiresRenderSnapshot &&
        Services.ParameterFactory.GetType() == typeof(DefaultSqlParameterFactory) &&
        Services.ObjectNameFormatter.GetType() == typeof(Bing.Data.Sql.Metadata.DefaultSqlObjectNameFormatter) &&
        Services.CrossDatabaseQueryValidator.GetType() == typeof(Bing.Data.Sql.Metadata.DefaultSqlCrossDatabaseQueryValidator) &&
        Services.TableReferenceValidator.GetType() == typeof(Bing.Data.Sql.Metadata.DefaultSqlTableReferenceValidator);

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

    /// <summary>
    /// 创建Select子句
    /// </summary>
    /// <returns>当前 Provider 创建的 Select 子句。</returns>
    protected virtual ISelectClause CreateSelectClause() => Provider.ClauseFactory.CreateSelect(CreateClauseContext());

    /// <summary>
    /// 创建From子句
    /// </summary>
    /// <returns>当前 Provider 创建的 From 子句。</returns>
    protected virtual IFromClause CreateFromClause() => Provider.ClauseFactory.CreateFrom(CreateClauseContext());

    /// <summary>
    /// 创建Join子句
    /// </summary>
    /// <returns>当前 Provider 创建的 Join 子句。</returns>
    protected virtual IJoinClause CreateJoinClause() => Provider.ClauseFactory.CreateJoin(CreateClauseContext());

    /// <summary>
    /// 创建Where子句
    /// </summary>
    /// <returns>当前 Provider 创建的 Where 子句。</returns>
    protected virtual IWhereClause CreateWhereClause() => Provider.ClauseFactory.CreateWhere(CreateClauseContext());

    /// <summary>
    /// 创建分组子句
    /// </summary>
    /// <returns>当前 Provider 创建的分组子句。</returns>
    protected virtual IGroupByClause CreateGroupByClause() => Provider.ClauseFactory.CreateGroupBy(CreateClauseContext());

    /// <summary>
    /// 创建排序子句
    /// </summary>
    /// <returns>当前 Provider 创建的排序子句。</returns>
    protected virtual IOrderByClause CreateOrderByClause() => Provider.ClauseFactory.CreateOrderBy(CreateClauseContext());

    /// <summary>
    /// 获取参数字面值解析器
    /// </summary>
    /// <returns>当前 SQL Provider 的参数字面值解析器。</returns>
    protected virtual IParamLiteralsResolver GetParamLiteralsResolver() => Provider.ParamLiteralsResolver;

    #endregion

    #region Clone(克隆)

    /// <inheritdoc />
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
        AutoGenerateTableAlias = sqlBuilder.AutoGenerateTableAlias;
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
        _hasQueryContent = sqlBuilder._hasQueryContent;
        FragmentParameterKeys = sqlBuilder.CloneFragmentParameterKeys();

        // 克隆各子句
        _selectClause = sqlBuilder._selectClause?.Clone(clonedContext);
        _fromClause = sqlBuilder._fromClause?.Clone(clonedContext);
        _joinClause = sqlBuilder._joinClause?.Clone(clonedContext);
        _whereClause = sqlBuilder._whereClause?.Clone(clonedContext);
        _groupByClause = sqlBuilder._groupByClause?.Clone(clonedContext);
        _orderByClause = sqlBuilder._orderByClause?.Clone(clonedContext);

        // 克隆分页信息
        Pager = new Pager(sqlBuilder.Pager.Page, sqlBuilder.Pager.PageSize, sqlBuilder.Pager.TotalCount,
            sqlBuilder.Pager.Order, sqlBuilder.Pager is Pager sourcePager && sourcePager.IsTotalCountKnown);
        OffsetParam = sqlBuilder.OffsetParam;
        LimitParam = sqlBuilder.LimitParam;
        if (sqlBuilder._executionRenderState?.FiltersApplied == true)
            _executionRenderState = new SqlQueryRenderState(ParameterManager, filtersApplied: true);
        _parentQueryContextId = sqlBuilder._parentQueryContextId;

        // 克隆集合
        _unionItems.Clear();
        _cteItems.Clear();
        CloneBuilderItems(_unionItems, sqlBuilder._unionItems, sqlBuilder);
        CloneBuilderItems(_cteItems, sqlBuilder._cteItems, sqlBuilder);
    }

    /// <summary>
    /// 克隆集合操作 Builder，并保留已冻结的子查询参数重命名关系。
    /// </summary>
    /// <param name="target">当前草稿持有的目标集合。</param>
    /// <param name="items">待克隆的集合操作项。</param>
    /// <param name="source">当前克隆的源 Builder。</param>
    private void CloneBuilderItems(ICollection<BuilderItem> target, IEnumerable<BuilderItem> items, SqlBuilderBase source)
    {
        foreach (var item in items)
        {
            if (item.Fragment != null)
            {
                var fragment = item.Fragment.Freeze().Render();
                target.Add(new BuilderItem(item.Name, fragment));
                if (source.SubqueryParameterNames.TryGetValue(item.Fragment, out var fragmentNames))
                    SubqueryParameterNames[fragment] = new Dictionary<string, string>(fragmentNames);
                continue;
            }
            var builder = item.Builder.Clone();
            target.Add(new BuilderItem(item.Name, builder,
                isOwnedNestedClone: !ReferenceEquals(item.Builder, builder)));
            if (source.SubqueryParameterNames.TryGetValue(item.Builder, out var names))
                SubqueryParameterNames[builder] = new Dictionary<string, string>(names);
        }
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
    /// 获取当前 Builder 引用的类型化子查询父上下文。
    /// </summary>
    internal string ParentQueryContextId => _parentQueryContextId;

    /// <summary>
    /// 渲染子查询。
    /// </summary>
    /// <param name="parentQueryContextId">父查询上下文标识。</param>
    internal void RegisterSubqueryParent(string parentQueryContextId)
    {
        if (string.IsNullOrWhiteSpace(_parentQueryContextId) && string.IsNullOrWhiteSpace(parentQueryContextId) == false)
            _parentQueryContextId = parentQueryContextId;
    }

    /// <summary>
    /// 渲染子查询。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <returns>参数名称已合并后的子查询 SQL。</returns>
    protected internal string RenderSubquery(ISqlBuilder builder)
    {
        return RenderSubquery(builder, ParameterManager, SubqueryParameterNames, isOwnedNestedClone: false);
    }

    /// <summary>
    /// 渲染由集合项提供的子查询，并在当前快照拥有该克隆时复用它。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="isOwnedNestedClone">子查询是否由当前集合项所在 Builder 克隆产生。</param>
    /// <returns>参数名称已合并后的子查询 SQL。</returns>
    internal string RenderSubquery(ISqlBuilder builder, bool isOwnedNestedClone) =>
        RenderSubquery(builder, ParameterManager, SubqueryParameterNames, isOwnedNestedClone);

    /// <summary>
    /// 渲染子查询。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="parameterManager">接收子查询参数的候选管理器。</param>
    /// <param name="subqueryParameterNames">接收子查询重命名结果的候选映射。</param>
    /// <param name="isOwnedNestedClone">子查询是否由当前快照拥有。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string RenderSubquery(ISqlBuilder builder, IParameterManager parameterManager,
        IDictionary<object, Dictionary<string, string>> subqueryParameterNames, bool isOwnedNestedClone)
    {
        var keys = FragmentParameterKeys;
        var sql = RenderSubquery(builder, parameterManager, subqueryParameterNames, ref keys, isOwnedNestedClone);
        FragmentParameterKeys = keys;
        return sql;
    }

    internal string RenderSubquery(ISqlBuilder builder, IParameterManager parameterManager,
        IDictionary<object, Dictionary<string, string>> subqueryParameterNames,
        ref Dictionary<string, SqlFragmentParameterBinding> parameterKeys, bool isOwnedNestedClone = false)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        if (subqueryParameterNames == null)
            throw new ArgumentNullException(nameof(subqueryParameterNames));
        // 父快照已经深克隆这些子树；继续递归 Clone 会让深层 CTE 重复复制。
        // Clause SPI 传入的其他 Builder 仍须在边界独立克隆。
        var snapshot = _renderOwnsNestedBuilders && isOwnedNestedClone ? builder : builder.Clone();
        var sql = snapshot is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSnapshot() : snapshot.ToSql();
        return MergeSubqueryParameters(builder, snapshot, sql, parameterManager, subqueryParameterNames, ref parameterKeys);
    }

    /// <summary>
    /// 合并子查询参数。
    /// </summary>
    /// <param name="builder">子查询生成器。</param>
    /// <param name="sql">已经生成的子查询 SQL 或条件 SQL。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string MergeSubqueryParameters(ISqlBuilder builder, string sql)
    {
        var keys = FragmentParameterKeys;
        var result = MergeSubqueryParameters(builder, builder, sql, ParameterManager, SubqueryParameterNames, ref keys);
        FragmentParameterKeys = keys;
        return result;
    }

    /// <summary>
    /// 渲染冻结 SQL 片段并将其参数合并到当前 Builder。
    /// </summary>
    /// <param name="fragment">冻结 SQL 片段。</param>
    /// <param name="rememberNames">是否为重复渲染保留本片段的参数名称。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    internal string RenderFragment(SqlQueryFragment fragment, bool rememberNames = false)
    {
        if (fragment == null)
            throw new ArgumentNullException(nameof(fragment));
        var parameters = fragment.Parameters;
        if (parameters.Count == 0)
            return fragment.Sql;

        var sourceParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var sourceSqlParameters = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            sourceParameters[parameter.Name] = parameter.Value;
            sourceSqlParameters[parameter.Name] = parameter;
        }

        var nameMap = rememberNames && SubqueryParameterNames.TryGetValue(fragment, out var existingNames)
            ? new Dictionary<string, string>(existingNames, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> distinctNames = null;
        var hasLogicalKeys = fragment.HasParameterKeys || FragmentParameterKeys != null;
        // 调用方可能覆盖已生成的参数；值或绑定合同不一致时必须分配新名称。
        foreach (var parameter in sourceParameters)
        {
            var targetName = nameMap.TryGetValue(parameter.Key, out var previousName) ? previousName : parameter.Key;
            if (ParameterManager.Contains(targetName) &&
                ((hasLogicalKeys && !HasCompatibleFragmentKey(fragment, parameter.Key, targetName)) ||
                 !IsFragmentParameterCompatible(ParameterManager, targetName, sourceSqlParameters[parameter.Key])))
            {
                nameMap.Remove(parameter.Key);
                (distinctNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(parameter.Key);
            }
        }
        PrepareSubqueryParameters(ParameterManager, sourceParameters, sourceSqlParameters, nameMap, distinctNames);
        if (sourceParameters.Count == 1)
        {
            var parameter = sourceParameters.First();
            var target = ParameterManager.NormalizeName(nameMap[parameter.Key]);
            if (string.Equals(parameter.Key, target, StringComparison.Ordinal) &&
                HasOnlyCanonicalFragmentTokens(fragment.Sql, parameter.Key))
            {
                AddSubqueryParameter(ParameterManager, sourceSqlParameters, parameter.Key, target, parameter.Value);
                if (hasLogicalKeys) RecordFragmentParameterKey(fragment, parameter.Key, target);
                if (rememberNames) SubqueryParameterNames[fragment] = nameMap;
                return fragment.Sql;
            }
        }
        // 参数管理器以不区分大小写的名称绑定；SQL 中只改写带前缀的标记，不能把普通列名当成参数。
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in nameMap.Keys)
        {
            var normalized = SqlQueryFragment.NormalizeName(name);
            counts.TryGetValue(normalized, out var count);
            counts[normalized] = count + 1;
        }
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in nameMap)
        {
            var name = item.Key.Trim();
            if (name.Length > 0 && name[0] is '@' or ':' or '?')
                tokens[name[0] + SqlQueryFragment.NormalizeName(name)] = ParameterManager.NormalizeName(item.Value);
        }
        foreach (var item in nameMap)
        {
            var name = item.Key.Trim();
            var normalized = SqlQueryFragment.NormalizeName(name);
            if (counts[normalized] != 1 && name.Length > 0 && name[0] is '@' or ':' or '?')
                continue;
            var targetName = ParameterManager.NormalizeName(item.Value);
            for (var index = 0; index < 3; index++)
            {
                var token = (index == 0 ? '@' : index == 1 ? ':' : '?') + normalized;
                if (!tokens.ContainsKey(token))
                    tokens.Add(token, targetName);
            }
        }
        var sql = ReplaceParameterTokens(fragment.Sql, tokens, ignoreCase: true);
        if (!string.Equals(sql, fragment.Sql, StringComparison.Ordinal) &&
            (fragment.Sql.Contains("\\'", StringComparison.Ordinal) || fragment.Sql.Contains("\\\"", StringComparison.Ordinal)))
            throw new InvalidOperationException("SQL 包含无法确定转义模式的引号，请统一参数拼写后重试。");
        foreach (var parameter in sourceParameters)
        {
            AddSubqueryParameter(ParameterManager, sourceSqlParameters, parameter.Key,
                nameMap[parameter.Key], parameter.Value);
            if (hasLogicalKeys) RecordFragmentParameterKey(fragment, parameter.Key, nameMap[parameter.Key]);
        }
        if (rememberNames)
            SubqueryParameterNames[fragment] = nameMap;
        return sql;
    }

    private bool HasCompatibleFragmentKey(SqlQueryFragment fragment, string sourceName, string targetName)
    {
        var hasSource = fragment.TryGetParameterKey(sourceName, out var sourceKey);
        var target = SqlQueryFragment.NormalizeName(ParameterManager.NormalizeName(targetName));
        var binding = default(SqlFragmentParameterBinding);
        var hasTarget = FragmentParameterKeys?.TryGetValue(target, out binding) == true &&
            binding.IsCurrent(ParameterManager, target);
        return hasSource == hasTarget && (!hasSource || sourceKey == binding.Key);
    }

    private void RecordFragmentParameterKey(SqlQueryFragment fragment, string sourceName, string targetName)
    {
        var target = SqlQueryFragment.NormalizeName(ParameterManager.NormalizeName(targetName));
        if (fragment.TryGetParameterKey(sourceName, out var key))
        {
            var keys = FragmentParameterKeys;
            if (keys == null)
                FragmentParameterKeys = keys = new Dictionary<string, SqlFragmentParameterBinding>(StringComparer.OrdinalIgnoreCase);
            keys[target] = new SqlFragmentParameterBinding(key, ParameterManager, target);
        }
        else
        {
            FragmentParameterKeys?.Remove(target);
        }
    }

    internal Dictionary<string, SqlFragmentParameterBinding> CloneFragmentParameterKeys()
    {
        if (FragmentParameterKeys == null)
            return null;
        Dictionary<string, SqlFragmentParameterBinding> result = null;
        foreach (var item in FragmentParameterKeys)
            if (item.Value.IsCurrent(ParameterManager, item.Key))
                (result ??= new Dictionary<string, SqlFragmentParameterBinding>(StringComparer.OrdinalIgnoreCase))[item.Key] = item.Value;
        return result;
    }

    /// <summary>参数按快照回放后，将保留的逻辑键绑定到新的描述符。</summary>
    private Dictionary<string, SqlFragmentParameterBinding> RebindFragmentParameterKeys(
        Dictionary<string, SqlFragmentParameterBinding> keys)
    {
        if (keys == null)
            return null;
        foreach (var name in keys.Keys.ToArray())
            keys[name] = new SqlFragmentParameterBinding(keys[name].Key, ParameterManager, name);
        return keys;
    }

    /// <summary>提交候选操作中与参数管理器配套的逻辑键。</summary>
    internal void ReplaceFragmentParameterKeys(Dictionary<string, SqlFragmentParameterBinding> keys) => FragmentParameterKeys = keys;

    internal bool HasFragmentParameterKeys => FragmentParameterKeys?.Count > 0;

    /// <summary>显式覆盖物理参数时，移除原片段对该名称的逻辑身份。</summary>
    internal void ForgetFragmentParameterKey(string name)
    {
        var keys = FragmentParameterKeys;
        if (keys == null)
            return;
        keys.Remove(SqlQueryFragment.NormalizeName(ParameterManager.NormalizeName(name)));
        if (keys.Count == 0)
            FragmentParameterKeys = null;
    }

    /// <summary>冻结时仅导出最终 SQL 仍引用的键，避免旧的重命名参数参与模板绑定。</summary>
    internal IReadOnlyDictionary<string, SqlParameterKey> GetFragmentParameterKeys(string sql)
        => GetFragmentParameterKeys(sql, ParameterManager, FragmentParameterKeys);

    /// <summary>从同次渲染的参数及逻辑绑定导出模板键，避免动态过滤写回草稿。</summary>
    internal static IReadOnlyDictionary<string, SqlParameterKey> GetFragmentParameterKeys(string sql,
        IParameterManager parameters, Dictionary<string, SqlFragmentParameterBinding> keys)
    {
        if (keys == null)
            return null;
        Dictionary<string, SqlParameterKey> result = null;
        foreach (var item in keys)
        {
            if (!item.Value.IsCurrent(parameters, item.Key) || !parameters.Contains(item.Key) ||
                !(ContainsParameterToken(sql, "@" + item.Key, true) ||
                  ContainsParameterToken(sql, ":" + item.Key, true) ||
                  ContainsParameterToken(sql, "?" + item.Key, true)))
                continue;
            (result ??= new Dictionary<string, SqlParameterKey>(StringComparer.OrdinalIgnoreCase))[item.Key] = item.Value.Key;
        }
        return result;
    }

    /// <summary>快速检查单参数片段是否完全采用目标标记；误命中只会回退到完整扫描。</summary>
    private static bool HasOnlyCanonicalFragmentTokens(string sql, string canonical)
    {
        var name = SqlQueryFragment.NormalizeName(canonical);
        if (name.Length == 0)
            return false;
        var index = 0;
        while ((index = sql.IndexOf(name, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (index > 0 && sql[index - 1] is '@' or ':' or '?' &&
                string.Compare(sql, index - 1, canonical, 0, canonical.Length, StringComparison.Ordinal) != 0)
                return false;
            index += name.Length;
        }
        return true;
    }

    /// <summary>比较片段加入后参数管理器实际保存的值及绑定合同。</summary>
    private bool IsFragmentParameterCompatible(IParameterManager target, string name, SqlParam source)
    {
        var storage = target is ParameterLimitManagerBase limited ? limited.GetStorage(out _, out _) : target;
        if (storage.GetType() == typeof(ParameterManager))
        {
            if (!SqlQueryShapeTemplate.ValuesEqual(target.GetValue(name), Dialect.GetParamValue(source.Value)))
                return false;
            return target is not IAdvancedParameterManager ||
                ((ParameterManager)storage).TryGetStoredSqlParam(name, out var existing) &&
                SqlQueryShapeTemplate.HasSameContract(existing, source) &&
                SqlQueryShapeTemplate.HasSameConversionState(existing, source);
        }

        // 外部管理器可自行转换值或元数据，在它的独立克隆上观察实际结果。
        var probe = CloneParameterManager(target);
        if (probe is IAdvancedParameterManager advancedProbe)
            advancedProbe.Add(CloneSqlParameter(source, name));
        else
            probe.Add(name, source.Value);
        if (!SqlQueryShapeTemplate.ValuesEqual(target.GetValue(name), probe.GetValue(name)))
            return false;
        if (target is not IAdvancedParameterManager advancedTarget || probe is not IAdvancedParameterManager expected)
            return true;
        var normalized = target.NormalizeName(name);
        return advancedTarget.GetSqlParams().TryGetValue(normalized, out var actualParameter) &&
            expected.GetSqlParams().TryGetValue(normalized, out var expectedParameter) &&
            SqlQueryShapeTemplate.HasSameContract(actualParameter, expectedParameter) &&
            SqlQueryShapeTemplate.HasSameConversionState(actualParameter, expectedParameter);
    }

    /// <summary>
    /// 合并子查询参数。
    /// </summary>
    /// <param name="builder">原始子查询生成器，用于缓存参数重命名关系。</param>
    /// <param name="parameterSource">与 SQL 同一渲染快照的参数来源。</param>
    /// <param name="sql">已经生成的子查询 SQL 或条件 SQL。</param>
    /// <param name="parameterManager">接收子查询参数的目标管理器。</param>
    /// <param name="subqueryParameterNames">接收子查询重命名结果的目标映射。</param>
    /// <param name="parameterKeys">与目标参数管理器配套的逻辑键。</param>
    /// <returns>参数名称已合并后的 SQL。</returns>
    private string MergeSubqueryParameters(ISqlBuilder builder, ISqlBuilder parameterSource, string sql,
        IParameterManager parameterManager,
        IDictionary<object, Dictionary<string, string>> subqueryParameterNames,
        ref Dictionary<string, SqlFragmentParameterBinding> parameterKeys)
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
        var sourceStorage = GetFrameworkParameterStorage(accessor.ParameterManager);
        var targetStorage = GetFrameworkParameterStorage(parameterManager);
        var alreadyConverted = sourceStorage != null && targetStorage != null &&
            sourceStorage.SharesDialectWith(targetStorage);
        var nameMap = subqueryParameterNames.TryGetValue(builder, out var existingNameMap)
            ? new Dictionary<string, string>(existingNameMap)
            : new Dictionary<string, string>();
        var sourceKeys = (parameterSource as SqlBuilderBase)?.GetFragmentParameterKeys(sql);
        var hasLogicalKeys = sourceKeys != null || parameterKeys != null;
        HashSet<string> distinctNames = null;
        if (hasLogicalKeys)
        {
            foreach (var parameter in sourceParameters)
            {
                var targetName = nameMap.TryGetValue(parameter.Key, out var previousName) ? previousName : parameter.Key;
                if (!parameterManager.Contains(targetName))
                    continue;
                var sourceKey = default(SqlParameterKey);
                var targetBinding = default(SqlFragmentParameterBinding);
                var hasSource = sourceKeys?.TryGetValue(SqlQueryFragment.NormalizeName(parameter.Key), out sourceKey) == true;
                var normalizedTarget = SqlQueryFragment.NormalizeName(parameterManager.NormalizeName(targetName));
                var hasTarget = parameterKeys?.TryGetValue(normalizedTarget, out targetBinding) == true &&
                    targetBinding.IsCurrent(parameterManager, normalizedTarget);
                if (hasSource == hasTarget && (!hasSource || sourceKey == targetBinding.Key) &&
                    (sourceSqlParameters != null && sourceSqlParameters.TryGetValue(parameter.Key, out var sourceParameter)
                        ? IsFragmentParameterCompatible(parameterManager, targetName, sourceParameter)
                        : SqlQueryShapeTemplate.ValuesEqual(parameterManager.GetValue(targetName), parameter.Value)))
                    continue;
                nameMap.Remove(parameter.Key);
                (distinctNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(parameter.Key);
            }
        }
        PrepareSubqueryParameters(parameterManager, sourceParameters, sourceSqlParameters, nameMap, distinctNames,
            alreadyConverted);
        if (sql.Contains("\\'", StringComparison.Ordinal) || sql.Contains("\\\"", StringComparison.Ordinal))
        {
            foreach (var item in nameMap)
            {
                if (!string.Equals(parameterManager.NormalizeName(item.Key),
                        parameterManager.NormalizeName(item.Value), StringComparison.Ordinal))
                    throw new InvalidOperationException("SQL 包含无法确定转义模式的引号，请统一参数拼写后重试。");
            }
        }
        var result = ReplaceParameterTokens(sql, nameMap);
        foreach (var parameter in sourceParameters)
        {
            AddSubqueryParameter(parameterManager, sourceSqlParameters, parameter.Key, nameMap[parameter.Key],
                parameter.Value, alreadyConverted);
            if (!hasLogicalKeys)
                continue;
            var targetName = SqlQueryFragment.NormalizeName(parameterManager.NormalizeName(nameMap[parameter.Key]));
            if (sourceKeys?.TryGetValue(SqlQueryFragment.NormalizeName(parameter.Key), out var key) == true)
                (parameterKeys ??= new Dictionary<string, SqlFragmentParameterBinding>(StringComparer.OrdinalIgnoreCase))[targetName] =
                    new SqlFragmentParameterBinding(key, parameterManager, targetName);
            else
                parameterKeys?.Remove(targetName);
        }
        subqueryParameterNames[builder] = nameMap;
        return result;
    }

    /// <summary>统一选择参数预检路径；内置容器只暂存新增项，第三方容器保留 Clone 合同。</summary>
    /// <param name="target">接收正式合并的参数管理器，预检期间不修改。</param>
    /// <param name="source">子查询参数值快照。</param>
    /// <param name="metadata">子查询参数元数据快照。</param>
    /// <param name="names">本次合并候选的参数重命名映射。</param>
    /// <param name="distinctNames">必须与目标现有参数隔离的源参数名称。</param>
    private void PrepareSubqueryParameters(IParameterManager target,
        IReadOnlyDictionary<string, object> source, IReadOnlyDictionary<string, SqlParam> metadata,
        IDictionary<string, string> names, ISet<string> distinctNames = null, bool alreadyConverted = false)
    {
        var storage = target;
        var maxCount = int.MaxValue;
        string providerKey = null;
        if (target is ParameterLimitManagerBase limited)
            storage = limited.GetStorage(out maxCount, out providerKey);
        if (storage.GetType() == typeof(ParameterManager))
        {
            ((ParameterManager)storage).PrepareSubqueryMerge(source, metadata, names,
                target is IAdvancedParameterManager, maxCount, providerKey, distinctNames, alreadyConverted);
            return;
        }

        var probe = target.Clone();
        foreach (var parameter in source)
        {
            if (!names.TryGetValue(parameter.Key, out var targetName))
            {
                targetName = parameter.Key;
                if (probe.Contains(parameter.Key))
                {
                    var incompatible = distinctNames?.Contains(parameter.Key) == true ||
                        (metadata != null && metadata.TryGetValue(parameter.Key, out var sourceParameter)
                            ? !IsFragmentParameterCompatible(probe, parameter.Key, sourceParameter)
                            : !Equals(probe.GetValue(parameter.Key), parameter.Value));
                    if (incompatible)
                        targetName = probe.GenerateName();
                }
                names[parameter.Key] = targetName;
            }
            AddSubqueryParameter(probe, metadata, parameter.Key, targetName, parameter.Value, alreadyConverted);
        }
    }

    /// <summary>
    /// 创建当前子查询参数重命名映射的候选副本。
    /// </summary>
    /// <returns>独立的子查询参数重命名映射。</returns>
    internal Dictionary<object, Dictionary<string, string>> CloneSubqueryParameterNames() =>
        SubqueryParameterNames.ToDictionary(item => item.Key,
            item => new Dictionary<string, string>(item.Value), ReferenceComparer<object>.Instance);

    /// <summary>
    /// 以一次状态替换子查询参数重命名映射。
    /// </summary>
    /// <param name="subqueryParameterNames">新的完整映射。</param>
    internal void ReplaceSubqueryParameterNames(
        IReadOnlyDictionary<object, Dictionary<string, string>> subqueryParameterNames)
    {
        if (subqueryParameterNames == null)
            throw new ArgumentNullException(nameof(subqueryParameterNames));
        SubqueryParameterNames.Clear();
        foreach (var item in subqueryParameterNames)
            SubqueryParameterNames[item.Key] = new Dictionary<string, string>(item.Value);
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
        var parameterNameSnapshot = SubqueryParameterNames.ToDictionary(item => item.Key,
            item => new Dictionary<string, string>(item.Value), ReferenceComparer<object>.Instance);
        var parameterKeySnapshot = CloneFragmentParameterKeys();
        try
        {
            action();
        }
        catch
        {
            RestoreQueryRenderSnapshot(parameterSnapshot, parameterNameSnapshot);
            FragmentParameterKeys = RebindFragmentParameterKeys(parameterKeySnapshot);
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
        IReadOnlyDictionary<string, SqlParam> sourceSqlParameters, string sourceName, string targetName, object value,
        bool alreadyConverted = false)
    {
        if (target.Contains(targetName))
            return;
        if (alreadyConverted)
        {
            var snapshot = sourceSqlParameters != null && sourceSqlParameters.TryGetValue(sourceName, out var source)
                ? CloneSqlParameter(source, targetName)
                : new SqlParam(targetName, value)
                {
                    Source = SqlParameterSource.Basic,
                    MetadataLevel = SqlParameterMetadataLevel.Weak
                };
            if (target is ParameterLimitManagerBase limited)
                limited.AddSnapshot(snapshot, target is IAdvancedParameterManager);
            else
                ((ParameterManager)target).AddSnapshot(snapshot, target is IAdvancedParameterManager);
            return;
        }
        if (sourceSqlParameters != null && sourceSqlParameters.TryGetValue(sourceName, out var sqlParameter) &&
            target is IAdvancedParameterManager advancedParameterManager)
        {
            advancedParameterManager.Add(CloneSqlParameter(sqlParameter, targetName));
            return;
        }
        target.Add(targetName, value);
    }

    private static ParameterManager GetFrameworkParameterStorage(IParameterManager manager)
    {
        var storage = manager is ParameterLimitManagerBase limited
            ? limited.GetStorage(out _, out _)
            : manager;
        return storage.GetType() == typeof(ParameterManager) ? (ParameterManager)storage : null;
    }

    /// <summary>
    /// 在 SQL 代码片段中替换独立参数标记，同时保留字符串、注释和标识符中的原始文本。
    /// </summary>
    /// <param name="sql">待处理的 SQL 文本。</param>
    /// <param name="parameterNames">源参数名称与目标参数名称的映射，均包含方言前缀。</param>
    /// <param name="ignoreCase">是否忽略源参数名称的大小写。</param>
    /// <param name="boundaries">可选的 SQL 结构边界；替换时同步调整位置，边界不能位于参数标记内部。</param>
    /// <returns>仅替换实际参数标记后的 SQL 文本。</returns>
    internal static string ReplaceParameterTokens(string sql, IReadOnlyDictionary<string, string> parameterNames,
        bool ignoreCase = false, int[] boundaries = null)
    {
        if (string.IsNullOrEmpty(sql) || parameterNames == null || parameterNames.Count == 0)
            return sql;

        // 常见作用域参数只包含字母、数字和下划线；按完整标记查表，避免每次创建整棵 Trie。
        var useNameLookup = ignoreCase &&
            parameterNames is Dictionary<string, string> dictionary &&
            dictionary.Comparer.Equals(StringComparer.OrdinalIgnoreCase) &&
            HasSimpleParameterNames(parameterNames);
        var replacementTrie = ignoreCase ? null : ParameterTokenReplacementTrie.Create(parameterNames);
        if (!ignoreCase && replacementTrie == null)
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
            if ((ignoreCase
                    ? (useNameLookup
                        ? TryGetParameterByName(sql, index, parameterNames, out var parameterName, out var replacement)
                        : TryGetParameterIgnoringCase(sql, index, parameterNames, out parameterName, out replacement))
                    : replacementTrie.TryGetValue(sql, index, out parameterName, out replacement)) &&
                IsParameterToken(sql, index, parameterName, ignoreCase))
            {
                if (boundaries != null)
                {
                    var tokenEnd = result.Length + parameterName.Length;
                    var difference = replacement.Length - parameterName.Length;
                    for (var boundary = 0; boundary < boundaries.Length; boundary++)
                        if (boundaries[boundary] >= tokenEnd)
                            boundaries[boundary] += difference;
                }
                result.Append(replacement);
                index += parameterName.Length;
                continue;
            }
            result.Append(current);
            index++;
        }
        return result.ToString();
    }

    private static bool HasSimpleParameterNames(IReadOnlyDictionary<string, string> parameterNames)
    {
        foreach (var name in parameterNames.Keys)
        {
            if (string.IsNullOrEmpty(name) || name[0] is not ('@' or ':' or '?'))
                return false;
            for (var index = 1; index < name.Length; index++)
                if (!IsParameterNameCharacter(name[index]))
                    return false;
        }
        return true;
    }

    private static bool TryGetParameterByName(string sql, int index,
        IReadOnlyDictionary<string, string> parameterNames, out string parameterName, out string replacement)
    {
        parameterName = null;
        replacement = null;
        if (sql[index] is not ('@' or ':' or '?'))
            return false;
        var end = index + 1;
        while (end < sql.Length && IsParameterNameCharacter(sql[end]))
            end++;
        if (end == index + 1)
            return false;
        parameterName = sql.Substring(index, end - index);
        return parameterNames.TryGetValue(parameterName, out replacement);
    }

    private static bool TryGetParameterIgnoringCase(string sql, int index,
        IReadOnlyDictionary<string, string> parameterNames, out string parameterName, out string replacement)
    {
        parameterName = null;
        replacement = null;
        if (sql[index] is not ('@' or ':' or '?'))
            return false;
        // 特殊名称沿用 OrdinalIgnoreCase 的完整 Unicode 语义，并优先匹配更长的标记。
        foreach (var pair in parameterNames)
        {
            if (pair.Key.Length <= (parameterName?.Length ?? 0) ||
                !IsParameterToken(sql, index, pair.Key, ignoreCase: true))
                continue;
            parameterName = pair.Key;
            replacement = pair.Value;
        }
        return parameterName != null;
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
    /// <param name="ignoreCase">是否忽略参数名称的大小写。</param>
    /// <returns>代码上下文包含该参数标记时返回 true。</returns>
    internal static bool ContainsParameterToken(string sql, string parameterName, bool ignoreCase = false)
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
            if (IsParameterToken(sql, index, parameterName, ignoreCase))
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
    /// 追加块注释片段，并处理支持嵌套注释的方言。
    /// </summary>
    /// <param name="sql">完整 SQL 文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">注释起始位置。</param>
    /// <returns>下一个待处理字符的位置。</returns>
    private static int AppendBlockComment(string sql, StringBuilder result, int index)
    {
        var depth = 0;
        while (index < sql.Length)
        {
            var current = sql[index++];
            result.Append(current);
            if (current == '/' && index < sql.Length && sql[index] == '*')
            {
                result.Append(sql[index++]);
                depth++;
                continue;
            }
            if (current == '*' && index < sql.Length && sql[index] == '/')
            {
                result.Append(sql[index++]);
                if (--depth == 0)
                    return index;
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
    /// <param name="ignoreCase">是否忽略参数名称的大小写。</param>
    /// <returns>是独立参数标记时返回 true。</returns>
    private static bool IsParameterToken(string sql, int index, string parameterName, bool ignoreCase = false)
    {
        if (index + parameterName.Length > sql.Length ||
            string.Compare(sql, index, parameterName, 0, parameterName.Length,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) != 0)
            return false;
        if (index > 0 && IsParameterNameCharacter(sql[index - 1]))
            return false;
        if (index + parameterName.Length < sql.Length && IsParameterNameCharacter(sql[index + parameterName.Length]))
            return false;
        if (parameterName[0] == '@' && index > 0 && sql[index - 1] == '@')
            return false;
        if (parameterName[0] == ':' && index > 0 && sql[index - 1] == ':')
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
    /// <returns>使用目标参数名创建的参数副本。</returns>
    private static SqlParam CloneSqlParameter(SqlParam parameter, string name)
    {
        return new SqlParam(name, parameter.Value, parameter.DbType, parameter.Direction, parameter.Size,
            parameter.Precision, parameter.Scale)
        {
            OriginalValue = parameter.OriginalValue,
            IsProviderValue = parameter.IsProviderValue,
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

    /// <inheritdoc />
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
        _executionRenderState = null;
        ClearSqlParams();
        ClearPageParams();
        ClearUnionBuilders();
        ClearCte();
        SubqueryParameterNames.Clear();
        _hasQueryContent = false;
        return this;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ISqlBuilder ClearFrom()
    {
        var fromClause = CreateFromClause();
        if (_fromClause != null) FromClause.Clear();
        _fromClause = fromClause;
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearJoin()
    {
        var joinClause = CreateJoinClause();
        if (_joinClause != null) JoinClause.Clear();
        _joinClause = joinClause;
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearWhere()
    {
        var whereClause = CreateWhereClause();
        _executionRenderState = null;
        _whereClause = whereClause;
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearGroupBy()
    {
        _groupByClause = CreateGroupByClause();
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearOrderBy()
    {
        _orderByClause = CreateOrderByClause();
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearSqlParams()
    {
        ParameterManager.Clear();
        FragmentParameterKeys = null;
        return this;
    }

    /// <inheritdoc />
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
        var parameterKeys = CloneFragmentParameterKeys();
        foreach (var name in names)
            ParameterManager.Remove(name);
        if (parameterKeys != null)
            foreach (var name in names)
                parameterKeys.Remove(SqlQueryFragment.NormalizeName(ParameterManager.NormalizeName(name)));
        FragmentParameterKeys = RebindFragmentParameterKeys(parameterKeys);
    }

    /// <inheritdoc />
    public ISqlBuilder ClearUnionBuilders()
    {
        foreach (var item in _unionItems)
            if (item.Fragment != null) SubqueryParameterNames.Remove(item.Fragment);
        _unionItems.Clear();
        return this;
    }

    /// <inheritdoc />
    public ISqlBuilder ClearCte()
    {
        foreach (var item in _cteItems)
            if (item.Fragment != null) SubqueryParameterNames.Remove(item.Fragment);
        _cteItems.Clear();
        return this;
    }

    #endregion

    #region New(创建Sql生成器)

    /// <inheritdoc />
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
        AutoGenerateTableAlias = source.AutoGenerateTableAlias;
        ExecutionContext = source.ExecutionContext;
        QueryCapabilities = source.QueryCapabilities;
        EntityResolver = new EntityResolver(EntityMappingResolver, DatabaseContextAccessor, MetadataOptions, Options,
            DatabaseContextResolver, EntityModelMetadataProvider, ExecutionContext.DatabaseContext);
    }

    #endregion

    #region ToSql(生成Sql)

    /// <inheritdoc />
    public virtual string ToSql()
    {
        if (ShouldRenderFromSnapshot() && !CanRenderFiltersLocally())
            // 此处已进入调用方的虚方法链，base 仅生成基础 SQL，由调用方追加自己的扩展。
            return ((SqlBuilderBase)Clone()).RenderSnapshot(includeOverrides: false);
        return ToSqlDirectly();
    }

    /// <summary>
    /// 创建查询执行使用的渲染快照，确保 SQL 文本与参数来源来自同一个 Builder。
    /// </summary>
    /// <remarks>
    /// 默认查询将动态过滤保存在局部工作区；无法从冻结结构派生的扩展保留独立规划副本。
    /// </remarks>
    /// <returns>当前 SQL 和对应参数 Builder 的渲染快照。</returns>
    internal SqlBuilderRenderSnapshot CreateRenderSnapshot()
    {
        var builder = ShouldRenderFromSnapshot() && !CanRenderFiltersLocally() ? (SqlBuilderBase)Clone() : this;
        var capture = new SqlBuilderRenderSnapshot(builder);
        var previous = builder._renderCapture;
        builder._renderCapture = capture;
        try
        {
            capture.Complete(ReferenceEquals(builder, this) ? ToSql() : builder.RenderSnapshot());
            if (capture.FilterState != null && (capture.SelectShape?.Order == null ||
                !capture.SelectShape.HasPage && !string.IsNullOrWhiteSpace(capture.SelectShape.LimitParameter)))
            {
                // 自定义 SQL 或分页形状仍需 Builder 回退；保留同次过滤，派生时不能重新取值。
                capture.Builder = builder.CreateFilteredPlanningSnapshot(capture.FilterState);
                capture.FilterState = null;
            }
            return capture;
        }
        finally
        {
            builder._renderCapture = previous;
        }
    }

    /// <summary>仅在冻结结构不能完整派生 Count/Page 时，将局部过滤提升为独立规划来源。</summary>
    private SqlBuilderBase CreateFilteredPlanningSnapshot(SqlQueryRenderState state)
    {
        var snapshot = (SqlBuilderBase)Clone();
        snapshot._parameterManager = state.Parameters;
        snapshot._parameterManagerState.Current = state.Parameters;
        var context = snapshot.CreateClauseContext();
        if (state.From != null)
            snapshot._fromClause = state.From.Clone(context);
        snapshot._whereClause = state.Where?.Clone(context);
        snapshot._joinClause = state.Join?.Clone(context);
        snapshot._orderByClause = state.OrderBy?.Clone(context);
        snapshot._offsetParam = state.OffsetParam;
        snapshot._limitParam = state.LimitParam;
        snapshot.ReplaceSubqueryParameterNames(state.SubqueryParameterNames);
        snapshot._draftParameterKeys = state.ParameterKeys;
        snapshot._executionRenderState = new SqlQueryRenderState(state.Parameters, filtersApplied: true);
        return snapshot;
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
        return snapshot;
    }

    /// <summary>
    /// 判断当前语句是否需要从独立 Builder 快照渲染。
    /// </summary>
    /// <returns>需要使用独立快照时返回 true。</returns>
    private bool ShouldRenderFromSnapshot() =>
        ShouldApplyFiltersOnRender();

    /// <summary>默认子句可从冻结结构派生 Count/Page；自定义扩展保留独立规划副本。</summary>
    private bool CanRenderFiltersLocally() => SupportsMutationCheckpoint && _mutationCheckpoint == null &&
        UsesFrameworkParameterManager(ParameterManager) && FrozenPaginationRenderer != null &&
        AliasRegister.GetType() == typeof(EntityAliasRegister);

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
    /// <param name="includeOverrides">快照入口是否需要同时生成第三方 ToSql 扩展。</param>
    /// <returns>与当前快照参数集合严格对应的 SQL。</returns>
    private string RenderSnapshot(bool includeOverrides = true)
    {
        var previousOwnership = _renderOwnsNestedBuilders;
        _renderOwnsNestedBuilders = true;
        try
        {
            if (ShouldApplyFiltersOnRender())
                EnsureFiltersAdded();
            // 过滤状态已固化，不会再次创建快照；仍走虚方法以保留第三方 SQL 扩展。
            return includeOverrides ? ToSql() : ToSqlDirectly();
        }
        finally
        {
            _renderOwnsNestedBuilders = previousOwnership;
        }
    }

    /// <summary>
    /// 判断当前语句是否需要在独立渲染快照中应用全局过滤器。
    /// </summary>
    /// <returns>需要应用动态过滤器时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private bool ShouldApplyFiltersOnRender()
    {
        if (_executionRenderState?.FiltersApplied == true || _activeRenderState?.FiltersApplied == true ||
            OperationKind != SqlOperationKind.Select)
            return false;
        var activeFilters = Services.Filters;
        if (activeFilters.Any(filter => filter is not IsDeletedFilter))
            return activeFilters.Count > 0;
        if (activeFilters.Any(filter => filter is IsDeletedFilter) == false)
            return false;
        return GetFilterSources().Any(source => source.EntityType != null &&
            typeof(ISoftDelete).IsAssignableFrom(source.EntityType));
    }

    /// <summary>
    /// 获取当前查询中可由默认过滤器处理的根表和 Join 表来源。
    /// </summary>
    /// <returns>当前查询中可由默认过滤器处理的表来源集合。</returns>
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
    /// 根据当前分页排序设置初始化查询 Builder。
    /// </summary>
    public virtual void Init() => OrderByClause.OrderBy(Pager?.Order);

    /// <summary>
    /// 验证当前 Builder 的查询能力、数据来源和分页排序约束。
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
    /// 验证查询语法能力。
    /// </summary>
    private void ValidateQueryCapabilities()
    {
        if (_cteItems.Count > 0)
            ValidateQueryCapability(QueryCapabilities.Cte, "CTE");
        foreach (var item in _unionItems)
            ValidateQueryCapability(item.Name);
        if (JoinClause is JoinClause joinClause && joinClause.ContainsJoinType("Right Join"))
            ValidateQueryCapability(QueryCapabilities.RightJoin, "Right Join");
        if (JoinClause is JoinClause fullJoinClause && fullJoinClause.ContainsJoinType("Full Join"))
            ValidateQueryCapability(QueryCapabilities.FullJoin, "Full Join");
        if (IsLimit)
            ValidateQueryCapability(QueryCapabilities.Pagination, "分页");
    }

    /// <summary>
    /// 验证查询语法能力。
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
    /// 验证查询语法能力。
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
    /// 验证查询语法能力。
    /// </summary>
    /// <param name="capability">能力状态。</param>
    /// <param name="name">语法名称。</param>
    private void ValidateQueryCapability(SqlQueryCapabilityState capability, string name)
    {
        if (capability == SqlQueryCapabilityState.Supported)
            return;
        var reason = SqlProviderCapabilityResolver.HasProfile(Provider) == false
            ? SqlCapabilityFailureReason.ProviderProfileMissing
            : SqlProviderCapabilityResolver.HasCompleteProfile(Provider)
                ? SqlCapabilityFailureReason.ProviderImplementationGap
                : SqlCapabilityFailureReason.ProviderProfileMismatch;
        throw SqlCapabilityFailure.Create(reason, $"Query:{name}", Provider.Key,
            $"Provider {Provider.Key} 的当前查询能力配置不支持 {name}。");
    }

    /// <summary>
    /// 创建Sql语句
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateSql(StringBuilder result)
    {
        CreateCte(result);
        if (IsUnion)
            CreateSqlByUnion(result);
        else
            CreateSqlByNoUnion(result);
    }

    /// <summary>
    /// 恢复查询渲染前的参数和子查询参数重命名状态。
    /// </summary>
    /// <param name="parameterSnapshot">渲染前的参数副本。</param>
    /// <param name="parameterNameSnapshot">渲染前的子查询参数重命名映射。</param>
    private void RestoreQueryRenderSnapshot(IParameterManager parameterSnapshot,
        IReadOnlyDictionary<object, Dictionary<string, string>> parameterNameSnapshot)
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

        SubqueryParameterNames.Clear();
        foreach (var item in parameterNameSnapshot)
            SubqueryParameterNames[item.Key] = new Dictionary<string, string>(item.Value);
    }

    /// <summary>
    /// 创建CTE
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateCte(StringBuilder result)
    {
        if (_cteItems.Count == 0)
            return;

        var cte = new StringBuilder(_cteItems.Count * 100);
        cte.Append($"{GetCteKeyWord()} ");

        for (var i = 0; i < _cteItems.Count; i++)
        {
            var item = _cteItems[i];
            cte.AppendLine($"{Dialect.SafeName(item.Name)} ");
            var sql = item.Fragment == null
                ? RenderSubquery(item.Builder, item.IsOwnedNestedClone)
                : RenderFragment(item.Fragment, rememberNames: true);
            cte.Append($"As ({sql})");

            if (i < _cteItems.Count - 1)
                cte.AppendLine(",");
        }

        result.AppendLine(cte.ToString());
    }

    /// <summary>
    /// 获取CTE关键字
    /// </summary>
    /// <returns>当前 Builder 使用的 CTE 关键字。</returns>
    protected virtual string GetCteKeyWord() => "With";

    /// <summary>
    /// 创建Sql语句 - 联合
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected virtual void CreateSqlByUnion(StringBuilder result)
    {
        var start = result.Length;
        result.Append("(");
        AppendSelect(result);
        AppendFrom(result);
        AppendClause(result, JoinClause);
        AppendClause(result, WhereClause);
        AppendClause(result, GroupByClause);
        AppendSql(result, ")");
        foreach (var operation in _unionItems)
        {
            AppendSql(result, operation.Name);
            var sql = RenderUnionItem(operation);
            AppendSql(result, $"({sql}");
            AppendSql(result, ")");
        }

        AppendQueryTail(result, start, start, true);
    }

    /// <summary>渲染集合操作中的 Builder 或冻结片段，并合并其参数。</summary>
    /// <param name="operation">集合操作项。</param>
    /// <returns>参数名称已合并的查询 SQL。</returns>
    protected string RenderUnionItem(BuilderItem operation) => operation.Fragment == null
        ? RenderSubquery(operation.Builder, operation.IsOwnedNestedClone)
        : RenderFragment(operation.Fragment, rememberNames: true);

    /// <summary>
    /// 创建Sql语句
    /// </summary>
    /// <param name="result">Sql拼接</param>
    protected void CreateSqlByNoUnion(StringBuilder result)
    {
        var start = result.Length;
        AppendSelect(result);
        var fromStart = result.Length;
        AppendFrom(result);
        AppendClause(result, JoinClause);
        AppendClause(result, WhereClause);
        AppendClause(result, GroupByClause);
        var wrapCount = GroupByClause.IsGroup || SelectClause is SelectClause { IsDistinct: true } ||
                        SelectClause is SelectClause { HasAggregate: true };
        AppendQueryTail(result, start, fromStart, wrapCount);
    }

    /// <summary>追加外层排序和分页，并在同次渲染中冻结普通或集合查询的派生边界。</summary>
    /// <param name="result">本次 SQL 缓冲区。</param>
    /// <param name="start">主查询起点，不包含 CTE 声明。</param>
    /// <param name="fromStart">普通查询的 From 起点；包装计数时不使用。</param>
    /// <param name="wrapCount">计数是否包装完整主查询。</param>
    protected void AppendQueryTail(StringBuilder result, int start, int fromStart, bool wrapCount)
    {
        var sourceEnd = _renderCapture == null ? 0 : GetTrimmedLength(result, start);
        SqlQueryOrder order = null;
        if (_renderCapture != null && OrderByClause.GetType() == typeof(OrderByClause))
        {
            var orderStart = result.Length;
            order = ((OrderByClause)OrderByClause).AppendFrozenTo(result);
            if (result.Length != orderStart)
                result.AppendLine(" ");
        }
        else
            AppendClause(result, OrderByClause);
        var orderEnd = _renderCapture == null ? 0 : GetTrimmedLength(result, start);
        AppendLimit(result);
        if (_renderCapture != null && (start == 0 || _cteItems.Count > 0) &&
            SelectClause is SelectClause && FromClause is FromClause from)
        {
            var queryEnd = GetTrimmedLength(result, start);
            var hasPage = orderEnd > sourceEnd && queryEnd > orderEnd &&
                          !string.IsNullOrWhiteSpace(OffsetParam) && !string.IsNullOrWhiteSpace(LimitParam);
            _renderCapture.SelectShape = new SqlQuerySelectShape(start, fromStart, sourceEnd,
                queryEnd, wrapCount, wrapCount ? from.FormatSubqueryAlias("t") : null,
                OffsetParam, LimitParam, hasPage, (OrderByClause as OrderByClause)?.GetFrozenPageOrder(),
                orderEnd > sourceEnd, order, orderEnd);
        }
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
        if (_executionRenderState?.FiltersApplied == true || _activeRenderState?.FiltersApplied == true)
            return;

        if (_activeRenderState?.IsolatedFilters == true)
            _activeRenderState.FiltersApplied = true;
        else
            _executionRenderState = new SqlQueryRenderState(ParameterManager, filtersApplied: true);
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
            if (placement.PrefilterSource)
            {
                if (FromClause is FromClause from && from.Sources.Any(source => source.SourceId == predicate.SourceId))
                {
                    if (_activeRenderState?.IsolatedFilters == true && _activeRenderState.From == null)
                        _activeRenderState.From = from.Clone(CreateClauseContext());
                    ((FromClause)FromClause).AddSourceFilter(predicate.SourceId, predicate.Column, predicate.Value,
                        predicate.Operator);
                }
                else if (JoinClause is JoinClause sourceJoin)
                    sourceJoin.AddSourceFilter(predicate.SourceId, predicate.Column, predicate.Value, predicate.Operator);
                else
                    throw new InvalidOperationException($"未找到待预过滤的来源 {predicate.SourceId}。");
                continue;
            }
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
    /// <returns>按当前分页参数和 Provider 方言生成的分页 SQL。</returns>
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

    /// <inheritdoc />
    public virtual string GetCondition() => WhereClause.GetCondition();

    #endregion

    #region Pager(设置分页)

    /// <inheritdoc />
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
    /// <returns>跳过行数对应的参数名。</returns>
    protected string GetOffsetParam()
    {
        if (string.IsNullOrWhiteSpace(OffsetParam) == false)
            return OffsetParam;
        var parameter = ParameterManager.GenerateName();
        ParameterManager.Add(parameter, 0);
        OffsetParam = parameter;
        return parameter;
    }

    /// <inheritdoc />
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
    /// <returns>限制行数对应的参数名。</returns>
    protected string GetLimitParam()
    {
        if (string.IsNullOrWhiteSpace(LimitParam) == false)
            return LimitParam;
        LimitParam = ParameterManager.GenerateName();
        return LimitParam;
    }

    /// <inheritdoc />
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
        ValidateOperation(action);
        _hasQueryContent = true;
    }

    /// <summary>
    /// 验证操作状态转换，但不改变当前状态。
    /// </summary>
    /// <param name="action">即将执行的查询或 Mutation 操作。</param>
    private void ValidateOperation(SqlOperationAction action)
    {
        if (action is not (SqlOperationAction.Select or SqlOperationAction.QueryClause or SqlOperationAction.Paging))
            throw new InvalidOperationException("查询 Builder 只接受查询操作，请使用专用写入 Builder。");
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        builder.CheckNull(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            var applyFilters = ShouldRenderFromSnapshot();
            if (applyFilters && !CanRenderFiltersLocally())
            {
                var snapshot = (SqlBuilderBase)Clone();
                if (snapshot.ShouldApplyFiltersOnRender())
                    snapshot.EnsureFiltersAdded();
                snapshot.AppendToCore(builder);
                return;
            }
            AppendToCore(builder, applyFilters);
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
    /// <param name="builder">用于接收 SQL 的字符串生成器。</param>
    /// <param name="isolateFilters">是否将动态过滤仅写入本次渲染的子句副本。</param>
    private void AppendToCore(StringBuilder builder, bool isolateFilters = false)
    {
        if (!UsesFrameworkParameterManager(ParameterManager))
        {
            AppendLegacyRender(builder);
            return;
        }
        var startIndex = builder.Length;
        // 候选参数和名称映射只在渲染成功后发布，异常不需要清空并逐项回放原管理器。
        var state = new SqlQueryRenderState(CloneParameterManager(ParameterManager), _draftParameterNames,
            parameterKeys: _draftParameterKeys)
        { OffsetParam = OffsetParam, LimitParam = LimitParam, IsolatedFilters = isolateFilters };
        _activeRenderState = state;
        _parameterManagerState.Current = state.Parameters;
        try
        {
            state.OrderBy = _orderByClause?.Clone(CreateClauseContext());
            if (isolateFilters)
                EnsureFiltersAdded();
            AppendQuery(builder);
            TrimAppendedWhitespace(builder, startIndex);
            if (isolateFilters)
            {
                if (_renderCapture != null)
                {
                    _renderCapture.FilterState = state;
                }
                return;
            }
            _parameterManager = state.Parameters;
            _offsetParam = state.OffsetParam;
            _limitParam = state.LimitParam;
            _orderByClause = state.OrderBy;
            _draftParameterNames.Clear();
            foreach (var item in state.SubqueryParameterNames)
                _draftParameterNames[item.Key] = item.Value;
            _draftParameterKeys = state.ParameterKeys;
        }
        finally
        {
            _activeRenderState = null;
            _parameterManagerState.Current = _parameterManager;
        }
    }

    /// <summary>外部参数管理器可能含有调用方可调策略，保留其既有实例与复制/恢复合同。</summary>
    private void AppendLegacyRender(StringBuilder builder)
    {
        var parameters = CloneParameterManager(ParameterManager);
        var names = CloneSubqueryParameterNames();
        var keys = CloneFragmentParameterKeys();
        var order = _orderByClause?.Clone(CreateClauseContext());
        var offset = OffsetParam;
        var limit = LimitParam;
        var startIndex = builder.Length;
        try
        {
            AppendQuery(builder);
            TrimAppendedWhitespace(builder, startIndex);
        }
        catch
        {
            RestoreQueryRenderSnapshot(parameters, names);
            FragmentParameterKeys = RebindFragmentParameterKeys(keys);
            _orderByClause = order;
            OffsetParam = offset;
            LimitParam = limit;
            throw;
        }
    }

    /// <summary>隔离参数候选，并在第三方管理器违反 Clone 契约时停止发布。</summary>
    private static IParameterManager CloneParameterManager(IParameterManager source)
    {
        var copy = source.Clone();
        if (copy == null || ReferenceEquals(copy, source))
            throw new InvalidOperationException("参数管理器克隆必须返回独立且非空的实例。");
        return copy;
    }

    private static bool UsesFrameworkParameterManager(IParameterManager manager) =>
        manager.GetType() == typeof(ParameterManager) ||
        manager is ParameterLimitManagerBase limited && limited.UsesFrameworkParameterManager;

    /// <summary>
    /// 渲染查询 SQL，并在初始化或验证失败时恢复分页临时追加前的排序状态。
    /// </summary>
    /// <param name="builder">用于接收 SQL 的字符串生成器。</param>
    private void AppendQuery(StringBuilder builder)
    {
        Init();
        Validate();
        CreateSql(builder);
    }

    /// <summary>
    /// 移除本次 SQL 渲染追加片段末尾的空白字符。
    /// </summary>
    /// <param name="builder">字符串生成器。</param>
    /// <param name="startIndex">本次追加前的起始位置。</param>
    private static void TrimAppendedWhitespace(StringBuilder builder, int startIndex)
    {
        var length = GetTrimmedLength(builder, startIndex);
        if (length < builder.Length)
            builder.Remove(length, builder.Length - length);
    }

    private static int GetTrimmedLength(StringBuilder builder, int startIndex)
    {
        var length = builder.Length;
        while (length > startIndex && char.IsWhiteSpace(builder[length - 1]))
            length--;
        return length;
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
    /// <param name="builder">与 SQL 对应的参数来源 Builder。</param>
    public SqlBuilderRenderSnapshot(ISqlBuilder builder)
    {
        Builder = builder;
    }

    /// <summary>
    /// 获取渲染后的 SQL。
    /// </summary>
    public string Sql { get; private set; }

    /// <summary>本次渲染产生的不可变结构；自定义完整 SQL 输出仍使用原派生路径。</summary>
    internal SqlQuerySelectShape SelectShape { get; set; }

    /// <summary>仅在本次冻结期间保留过滤工作区，以便必要时构造兼容规划来源。</summary>
    internal SqlQueryRenderState FilterState { get; set; }
    internal IParameterManager Parameters => FilterState?.Parameters;

    internal IReadOnlyDictionary<string, SqlParameterKey> GetFragmentParameterKeys() => Parameters == null
        ? (Builder as SqlBuilderBase)?.GetFragmentParameterKeys(Sql)
        : SqlBuilderBase.GetFragmentParameterKeys(Sql, Parameters, FilterState.ParameterKeys);

    /// <summary>边界覆盖本次完整 SQL 时才发布结构，兼容自定义尾部 SQL。</summary>
    internal void Complete(string sql)
    {
        Sql = sql;
        if (SelectShape?.QueryEnd != sql.Length)
            SelectShape = null;
    }

    /// <summary>
    /// 获取与 SQL 对应的参数来源 Builder。
    /// </summary>
    public ISqlBuilder Builder { get; internal set; }
}

/// <summary>
/// 按对象实例比较引用的比较器。
/// </summary>
/// <typeparam name="T">引用类型。</typeparam>
internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    /// <summary>
    /// 获取比较器实例。
    /// </summary>
    public static ReferenceComparer<T> Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    /// <inheritdoc />
    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

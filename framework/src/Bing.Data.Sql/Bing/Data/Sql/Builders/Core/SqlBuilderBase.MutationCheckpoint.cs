using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

public abstract partial class SqlBuilderBase
{
    private MutationCheckpoint _mutationCheckpoint;

    /// <summary>框架内置 Builder 可为普通草稿操作启用按子句复制；派生类型默认保留完整 Clone 隔离。</summary>
    protected virtual bool SupportsMutationCheckpoint => false;

    internal MutationCheckpoint BeginMutationCheckpoint()
    {
        if (!SupportsMutationCheckpoint || _mutationCheckpoint != null || !UsesFrameworkParameterManager(ParameterManager))
            return null;
        // 外部 Clause 可以保存额外可变状态，仍由它的完整 Clone 契约隔离。
        if (new object[] { _selectClause, _fromClause, _joinClause, _whereClause, _groupByClause, _orderByClause }
            .Any(clause => clause != null && clause.GetType().Assembly != typeof(SqlBuilderBase).Assembly &&
                clause.GetType().Assembly != GetType().Assembly))
            return null;
        return _mutationCheckpoint = new MutationCheckpoint(this);
    }

    /// <summary>成功路径仅复制实际触及的子句；失败才重绑原子句的上下文。</summary>
    internal sealed class MutationCheckpoint : IDisposable
    {
        private readonly SqlBuilderBase _owner;
        private readonly IParameterManager _parameters;
        private readonly IEntityAliasRegister _aliases;
        private readonly List<BuilderItem> _unions;
        private readonly List<BuilderItem> _ctes;
        private readonly Dictionary<object, Dictionary<string, string>> _names;
        private readonly Dictionary<string, SqlFragmentParameterBinding> _keys;
        private readonly IPager _pager;
        private readonly string _offset;
        private readonly string _limit;
        private readonly bool _hasQuery;
        private readonly string _parent;
        private readonly SqlQueryRenderState _execution;
        private bool _completed;

        internal ISelectClause Select { get; }
        internal IFromClause From { get; }
        internal IJoinClause Join { get; }
        internal IWhereClause Where { get; }
        internal IGroupByClause GroupBy { get; }
        internal IOrderByClause OrderBy { get; }

        internal MutationCheckpoint(SqlBuilderBase owner)
        {
            _owner = owner;
            _parameters = CloneParameterManager(owner.ParameterManager);
            _aliases = owner.AliasRegister.Clone();
            _unions = owner.UnionItems.ToList();
            _ctes = owner.CteItems.ToList();
            _names = owner.CloneSubqueryParameterNames();
            _keys = owner.CloneFragmentParameterKeys();
            _pager = new Pager(owner.Pager.Page, owner.Pager.PageSize, owner.Pager.TotalCount,
                owner.Pager.Order, owner.Pager is Pager pager && pager.IsTotalCountKnown);
            _offset = owner.OffsetParam;
            _limit = owner.LimitParam;
            _hasQuery = owner._hasQueryContent;
            _parent = owner._parentQueryContextId;
            _execution = owner._executionRenderState;
            Select = owner._selectClause;
            From = owner._fromClause;
            Join = owner._joinClause;
            Where = owner._whereClause;
            GroupBy = owner._groupByClause;
            OrderBy = owner._orderByClause;
        }

        internal void Complete() => _completed = true;

        public void Dispose()
        {
            _owner._mutationCheckpoint = null;
            if (_completed) return;
            _owner.ReplaceParameterManager(_parameters);
            _owner.AliasRegister = _aliases;
            var context = _owner.CreateClauseContext();
            _owner._selectClause = Select?.Clone(context);
            _owner._fromClause = From?.Clone(context);
            _owner._joinClause = Join?.Clone(context);
            _owner._whereClause = Where?.Clone(context);
            _owner._groupByClause = GroupBy?.Clone(context);
            _owner._orderByClause = OrderBy?.Clone(context);
            _owner.UnionItems = _unions;
            _owner.CteItems = _ctes;
            _owner.ReplaceSubqueryParameterNames(_names);
            _owner.FragmentParameterKeys = _keys;
            _owner.Pager = _pager;
            _owner.OffsetParam = _offset;
            _owner.LimitParam = _limit;
            _owner._hasQueryContent = _hasQuery;
            _owner._parentQueryContextId = _parent;
            _owner._executionRenderState = _execution;
        }
    }
}

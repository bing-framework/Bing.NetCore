using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Update SQL Builder。
/// </summary>
public sealed class SqlUpdateBuilder : SqlMutationBuilderBase, ISqlUpdateBuilder
{
    /// <summary>
    /// 是否已将默认数据边界追加到当前 Where 子句。
    /// </summary>
    private bool _isDataBoundaryApplied;

    /// <summary>
    /// 初始化一个 <see cref="SqlUpdateBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">可共享的 Builder 服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="clauseFactory">Mutation Clause Factory。</param>
    public SqlUpdateBuilder(ISqlProvider provider, SqlBuilderServices services, IParameterManager parameterManager = null,
        ISqlMutationClauseFactory clauseFactory = null)
        : base(provider, services, parameterManager, clauseFactory)
    {
        UpdateClause = ClauseFactory.CreateUpdate(MutationContext);
        UpdateFromClause = CreateUpdateFromClause();
        SetClause = ClauseFactory.CreateSet(MutationContext);
        WhereClause = ClauseFactory.CreateWhere(MutationContext);
    }

    /// <inheritdoc />
    public IUpdateClause UpdateClause { get; private set; }

    /// <inheritdoc />
    public IUpdateFromClause UpdateFromClause { get; private set; }

    /// <inheritdoc />
    public ISetClause SetClause { get; private set; }

    /// <inheritdoc />
    public IMutationWhereClause WhereClause { get; private set; }

    /// <inheritdoc />
    public bool AllowAllRows { get; private set; }

    /// <inheritdoc />
    public void SetAllowAllRows(bool allowAllRows) => AllowAllRows = allowAllRows;

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            if (ShouldRenderDataBoundarySnapshot())
            {
                var snapshot = (SqlUpdateBuilder)Clone();
                snapshot.EnsureDataBoundary();
                snapshot.AppendCore(builder);
                return;
            }
            EnsureDataBoundary();
            AppendCore(builder);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <summary>
    /// 在当前状态下渲染 Update 子句，不追加新的数据边界。
    /// </summary>
    private void AppendCore(StringBuilder builder)
    {
        Validate();
        UpdateClause.AppendTo(builder);
        SetClause.AppendTo(builder);
        if (UpdateFromClause.Table != null)
            UpdateFromClause.AppendTo(builder);
        WhereClause.AppendTo(builder);
    }

    /// <inheritdoc />
    public string ToSql()
    {
        if (ShouldRenderDataBoundarySnapshot())
        {
            var snapshot = (SqlUpdateBuilder)Clone();
            snapshot.EnsureDataBoundary();
            return snapshot.RenderCore();
        }
        return RenderCore();
    }

    /// <inheritdoc />
    public SqlWriteCommand BuildCommand()
    {
        if (ShouldRenderDataBoundarySnapshot())
        {
            var snapshot = (SqlUpdateBuilder)Clone();
            snapshot.EnsureDataBoundary();
            return new SqlWriteCommand(snapshot.RenderCore(), snapshot.GetParameters());
        }
        return BuildCommand(RenderCore);
    }

    /// <inheritdoc />
    public ISqlUpdateBuilder New() => new SqlUpdateBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <inheritdoc />
    public ISqlUpdateBuilder Clone()
    {
        var result = new SqlUpdateBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.UpdateClause = UpdateClause.Clone(result.MutationContext);
        result.UpdateFromClause = UpdateFromClause.Clone(result.MutationContext);
        result.SetClause = SetClause.Clone(result.MutationContext);
        result.WhereClause = WhereClause.Clone(result.MutationContext);
        result.AllowAllRows = AllowAllRows;
        result._isDataBoundaryApplied = _isDataBoundaryApplied;
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        UpdateClause.Clear();
        UpdateFromClause.Clear();
        SetClause.Clear();
        WhereClause.Clear();
        AllowAllRows = false;
        _isDataBoundaryApplied = false;
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Update 结构。
    /// </summary>
    private void Validate()
    {
        var context = new SqlValidationContext(Provider, ParameterManager.Count, AllowAllRows, SqlExecutionKind.Update);
        UpdateClause.Validate(context);
        SetClause.Validate(context);
        if (UpdateFromClause.Table != null)
            UpdateFromClause.Validate(context);
        WhereClause.Validate(context);
        ValidateParameterLimit();
    }

    /// <summary>
    /// 在首次渲染前为结构化实体目标追加默认数据边界。
    /// </summary>
    private void EnsureDataBoundary()
    {
        if (_isDataBoundaryApplied)
            return;
        _isDataBoundaryApplied = SqlMutationDataBoundary.Apply(MutationContext, UpdateClause.Table, WhereClause);
    }

    /// <summary>
    /// 判断当前渲染是否需要使用独立副本追加数据边界。
    /// </summary>
    private bool ShouldRenderDataBoundarySnapshot() => _isDataBoundaryApplied == false &&
        SqlMutationDataBoundary.ShouldApply(MutationContext, UpdateClause.Table);

    /// <summary>
    /// 渲染当前 Update 状态。
    /// </summary>
    private string RenderCore() => Render(AppendCore);

    /// <summary>
    /// 创建 Update From 子句，优先使用 Provider 注册的专用子句工厂。
    /// </summary>
    /// <returns>Provider 专用或默认的 Update From 子句。</returns>
    private IUpdateFromClause CreateUpdateFromClause() => ClauseFactory is ISqlUpdateFromClauseFactory factory
        ? factory.CreateUpdateFrom(MutationContext)
        : new UpdateFromClause(MutationContext);
}
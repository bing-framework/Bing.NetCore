using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Delete SQL Builder。
/// </summary>
public sealed class SqlDeleteBuilder : SqlMutationBuilderBase, ISqlDeleteBuilder
{
    /// <summary>
    /// 是否已将默认数据边界追加到当前 Where 子句。
    /// </summary>
    private bool _isDataBoundaryApplied;

    /// <summary>
    /// 当前 Delete 对应的数据边界语义。
    /// </summary>
    internal SqlDataBoundaryOperation DataBoundaryOperation { get; set; } = SqlDataBoundaryOperation.Delete;

    /// <summary>
    /// 初始化一个 <see cref="SqlDeleteBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">可共享的 Builder 服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="clauseFactory">Mutation Clause Factory。</param>
    public SqlDeleteBuilder(ISqlProvider provider, SqlBuilderServices services, IParameterManager parameterManager = null,
        ISqlMutationClauseFactory clauseFactory = null)
        : base(provider, services, parameterManager, clauseFactory)
    {
        DeleteClause = ClauseFactory.CreateDelete(MutationContext);
        DeleteUsingClause = CreateDeleteUsingClause();
        WhereClause = ClauseFactory.CreateWhere(MutationContext);
    }

    /// <inheritdoc />
    public IDeleteClause DeleteClause { get; private set; }

    /// <inheritdoc />
    public IDeleteUsingClause DeleteUsingClause { get; private set; }

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
                var snapshot = (SqlDeleteBuilder)Clone();
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
    /// 在当前状态下渲染 Delete 子句，不追加新的数据边界。
    /// </summary>
    private void AppendCore(StringBuilder builder)
    {
        Validate();
        DeleteClause.AppendTo(builder);
        if (DeleteUsingClause.Table != null)
            DeleteUsingClause.AppendTo(builder);
        WhereClause.AppendTo(builder);
    }

    /// <inheritdoc />
    public string ToSql()
    {
        if (ShouldRenderDataBoundarySnapshot())
        {
            var snapshot = (SqlDeleteBuilder)Clone();
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
            var snapshot = (SqlDeleteBuilder)Clone();
            snapshot.EnsureDataBoundary();
            return snapshot.BuildCommand(snapshot.RenderCore);
        }
        return BuildCommand(RenderCore);
    }

    /// <inheritdoc />
    public ISqlDeleteBuilder New() => new SqlDeleteBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <inheritdoc />
    public ISqlDeleteBuilder Clone()
    {
        var result = new SqlDeleteBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.DeleteClause = DeleteClause.Clone(result.MutationContext);
        result.DeleteUsingClause = DeleteUsingClause.Clone(result.MutationContext);
        result.WhereClause = WhereClause.Clone(result.MutationContext);
        result.AllowAllRows = AllowAllRows;
        result._isDataBoundaryApplied = _isDataBoundaryApplied;
        result.DataBoundaryOperation = DataBoundaryOperation;
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        DeleteClause.Clear();
        DeleteUsingClause.Clear();
        WhereClause.Clear();
        AllowAllRows = false;
        _isDataBoundaryApplied = false;
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Delete 结构。
    /// </summary>
    private void Validate()
    {
        var context = new SqlValidationContext(Provider, ParameterManager.Count, AllowAllRows, SqlExecutionKind.Delete);
        DeleteClause.Validate(context);
        if (DeleteUsingClause.Table != null)
            DeleteUsingClause.Validate(context);
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
        _isDataBoundaryApplied = SqlMutationDataBoundary.Apply(MutationContext, DeleteClause.Table,
            DataBoundaryOperation, WhereClause);
    }

    /// <summary>
    /// 判断当前渲染是否需要使用独立副本追加数据边界。
    /// </summary>
    private bool ShouldRenderDataBoundarySnapshot() => _isDataBoundaryApplied == false &&
        SqlMutationDataBoundary.ShouldApply(MutationContext, DeleteClause.Table, DataBoundaryOperation);

    /// <summary>
    /// 渲染当前 Delete 状态。
    /// </summary>
    private string RenderCore() => Render(AppendCore);

    /// <summary>
    /// 创建 Delete Using 子句，优先使用 Provider 注册的专用子句工厂。
    /// </summary>
    /// <returns>Provider 专用或默认的 Delete Using 子句。</returns>
    private IDeleteUsingClause CreateDeleteUsingClause() => ClauseFactory is ISqlDeleteUsingClauseFactory factory
        ? factory.CreateDeleteUsing(MutationContext)
        : new DeleteUsingClause(MutationContext);
}
using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Delete SQL Builder。
/// </summary>
public sealed class SqlDeleteBuilder : SqlMutationBuilderBase, ISqlDeleteBuilder
{
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
        WhereClause = ClauseFactory.CreateWhere(MutationContext);
    }

    /// <inheritdoc />
    public IDeleteClause DeleteClause { get; private set; }

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
        Validate();
        DeleteClause.AppendTo(builder);
        WhereClause.AppendTo(builder);
    }

    /// <inheritdoc />
    public string ToSql() => Render(AppendTo);

    /// <inheritdoc />
    public ISqlDeleteBuilder New() => new SqlDeleteBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <inheritdoc />
    public ISqlDeleteBuilder Clone()
    {
        var result = new SqlDeleteBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.DeleteClause = DeleteClause.Clone(result.MutationContext);
        result.WhereClause = WhereClause.Clone(result.MutationContext);
        result.AllowAllRows = AllowAllRows;
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        DeleteClause.Clear();
        WhereClause.Clear();
        AllowAllRows = false;
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Delete 结构。
    /// </summary>
    private void Validate()
    {
        var context = new SqlValidationContext(Provider, GetParameters().Count, AllowAllRows, SqlExecutionKind.Delete);
        DeleteClause.Validate(context);
        WhereClause.Validate(context);
        ValidateParameterLimit();
    }
}
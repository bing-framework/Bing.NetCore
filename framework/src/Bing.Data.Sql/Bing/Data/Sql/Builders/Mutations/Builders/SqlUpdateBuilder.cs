using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Update SQL Builder。
/// </summary>
public sealed class SqlUpdateBuilder : SqlMutationBuilderBase, ISqlUpdateBuilder
{
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
        SetClause = ClauseFactory.CreateSet(MutationContext);
        WhereClause = ClauseFactory.CreateWhere(MutationContext);
    }

    /// <inheritdoc />
    public IUpdateClause UpdateClause { get; private set; }

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
        Validate();
        UpdateClause.AppendTo(builder);
        SetClause.AppendTo(builder);
        WhereClause.AppendTo(builder);
    }

    /// <inheritdoc />
    public string ToSql() => Render(AppendTo);

    /// <inheritdoc />
    public ISqlUpdateBuilder New() => new SqlUpdateBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <inheritdoc />
    public ISqlUpdateBuilder Clone()
    {
        var result = new SqlUpdateBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.UpdateClause = UpdateClause.Clone(result.MutationContext);
        result.SetClause = SetClause.Clone(result.MutationContext);
        result.WhereClause = WhereClause.Clone(result.MutationContext);
        result.AllowAllRows = AllowAllRows;
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        UpdateClause.Clear();
        SetClause.Clear();
        WhereClause.Clear();
        AllowAllRows = false;
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Update 结构。
    /// </summary>
    private void Validate()
    {
        var context = new SqlValidationContext(Provider, GetParameters().Count, AllowAllRows, SqlExecutionKind.Update);
        UpdateClause.Validate(context);
        SetClause.Validate(context);
        WhereClause.Validate(context);
        ValidateParameterLimit();
    }
}
using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// 默认 Insert SQL Builder。
/// </summary>
public sealed class SqlInsertBuilder : SqlMutationBuilderBase, ISqlInsertBuilder
{
    /// <summary>
    /// 初始化一个 <see cref="SqlInsertBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">可共享的 Builder 服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="clauseFactory">Mutation Clause Factory。</param>
    public SqlInsertBuilder(ISqlProvider provider, SqlBuilderServices services, IParameterManager parameterManager = null,
        ISqlMutationClauseFactory clauseFactory = null)
        : base(provider, services, parameterManager, clauseFactory)
    {
        InsertClause = ClauseFactory.CreateInsert(MutationContext);
        InsertColumnsClause = ClauseFactory.CreateInsertColumns(MutationContext);
        ValuesClause = ClauseFactory.CreateValues(MutationContext);
    }

    /// <inheritdoc />
    public IInsertClause InsertClause { get; private set; }

    /// <inheritdoc />
    public IInsertColumnsClause InsertColumnsClause { get; private set; }

    /// <inheritdoc />
    public IValuesClause ValuesClause { get; private set; }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            Validate();
            InsertClause.AppendTo(builder);
            InsertColumnsClause.AppendTo(builder);
            ValuesClause.AppendTo(builder);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public string ToSql() => Render(AppendTo);

    /// <inheritdoc />
    public SqlWriteCommand BuildCommand() => BuildCommand(ToSql);

    /// <inheritdoc />
    public ISqlInsertBuilder New() => new SqlInsertBuilder(Provider, MutationContext.Services,
        ParameterManager.CreateEmpty(), ClauseFactory);

    /// <inheritdoc />
    public ISqlInsertBuilder Clone()
    {
        var result = new SqlInsertBuilder(Provider, MutationContext.Services, ParameterManager.Clone(), ClauseFactory);
        result.InsertClause = InsertClause.Clone(result.MutationContext);
        result.InsertColumnsClause = InsertColumnsClause.Clone(result.MutationContext);
        result.ValuesClause = ValuesClause.Clone(result.MutationContext);
        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        InsertClause.Clear();
        InsertColumnsClause.Clear();
        ValuesClause.Clear();
        ParameterManager.Clear();
    }

    /// <summary>
    /// 验证 Insert 结构。
    /// </summary>
    private void Validate()
    {
        var context = new SqlValidationContext(Provider, ParameterManager.Count, false, SqlExecutionKind.Insert);
        InsertClause.Validate(context);
        InsertColumnsClause.Validate(context);
        ValuesClause.Validate(context);
        if (InsertColumnsClause.Columns.Count != ValuesClause.ColumnCount)
            throw new InvalidOperationException("Insert 列数量与 Values 列数量不一致。");
        ValidateParameterLimit();
    }
}
using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 指定结果类型的存储过程查询描述。
/// </summary>
/// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
/// <remarks>
/// 该描述复用原生文本查询的终结方法，但固定以 <see cref="CommandType.StoredProcedure"/> 执行。
/// 传入的非字典参数对象保持原引用，以便 Dapper 输出参数在执行后回写到调用方对象。
/// </remarks>
public sealed class SqlProcedureQuery<TResult> : SqlTextQuery<TResult>
{
    /// <summary>
    /// 使用根查询、存储过程名称和参数源初始化查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="procedure">要执行的存储过程名称。</param>
    /// <param name="parameters">由参数绑定器处理的输入和输出参数源。</param>
    internal SqlProcedureQuery(ISqlQueryPlanExecutor executor, string procedure, object parameters)
        : base(executor, procedure, parameters)
    {
    }

    /// <summary>
    /// 获取最近一次过程执行后可读取的输出参数访问器。
    /// </summary>
    /// <remarks>
    /// 仅当参数源使用框架 <see cref="SqlParameterCollection"/>、<see cref="SqlParameterMap{TEntity}"/> 等可绑定输出参数的模型，
    /// 且过程已完成执行后，访问器才可用。
    /// </remarks>
    public ISqlOutputParameterAccessor OutputParameters => Executor.OutputParameters;

    /// <inheritdoc />
    private protected override SqlQueryPlan GetPlan() => SqlQueryPlan.Create(CommandText, Parameters, SplitOnColumn,
        System.Data.CommandType.StoredProcedure);
}
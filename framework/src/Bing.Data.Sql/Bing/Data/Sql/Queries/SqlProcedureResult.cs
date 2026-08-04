namespace Bing.Data.Sql;

/// <summary>
/// 表示一次存储过程终结执行的结果及其输出参数。
/// </summary>
/// <typeparam name="TResult">本次过程执行返回的结果类型。</typeparam>
/// <remarks>
/// 输出参数访问器与本次执行绑定，不依赖 Root Query 或 Executor 的最近一次可变状态。
/// 当参数源不是框架支持的输出参数模型时，<see cref="OutputParameters"/> 为 <see langword="null"/>，
/// 调用方应直接使用其参数源所定义的访问方式。
/// </remarks>
public sealed class SqlProcedureResult<TResult>
{
    /// <summary>
    /// 初始化一次存储过程执行结果。
    /// </summary>
    /// <param name="result">本次执行返回的结果。</param>
    /// <param name="outputParameters">本次执行绑定的输出参数访问器。</param>
    public SqlProcedureResult(TResult result, ISqlOutputParameterAccessor outputParameters)
    {
        Result = result;
        OutputParameters = outputParameters;
    }

    /// <summary>
    /// 获取本次过程执行返回的结果。
    /// </summary>
    public TResult Result { get; }

    /// <summary>
    /// 获取本次过程执行完成后可读取的输出参数访问器；不支持框架输出参数模型时为 <see langword="null"/>。
    /// </summary>
    public ISqlOutputParameterAccessor OutputParameters { get; }
}
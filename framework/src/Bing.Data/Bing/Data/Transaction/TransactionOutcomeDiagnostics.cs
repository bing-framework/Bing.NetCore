namespace Bing.Data.Transaction;

/// <summary>
/// 在原始异常上记录数据库事务结果。
/// </summary>
/// <remarks>提交结果未知时应查询业务幂等结果，不能直接重放业务。记录诊断不改变异常类型。</remarks>
public static class TransactionOutcomeDiagnostics
{
    /// <summary>
    /// 异常中的事务结果键。
    /// </summary>
    private const string OutcomeKey = "Bing.Transaction.Outcome";

    /// <summary>
    /// 读取异常关联的事务结果。
    /// </summary>
    /// <param name="exception">事务异常。</param>
    /// <returns>已记录的事务结果；没有记录时返回未知。</returns>
    public static TransactionOutcome GetTransactionOutcome(this Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
        return exception.Data[OutcomeKey] is TransactionOutcome outcome
            ? outcome
            : TransactionOutcome.Unknown;
    }

    /// <summary>
    /// 记录异常关联的事务结果。
    /// </summary>
    /// <param name="exception">保留类型及实例的原始异常。</param>
    /// <param name="outcome">能够确认的事务结果。</param>
    public static void SetTransactionOutcome(this Exception exception, TransactionOutcome outcome)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
        if (!Enum.IsDefined(typeof(TransactionOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        exception.Data[OutcomeKey] = outcome;
    }
}

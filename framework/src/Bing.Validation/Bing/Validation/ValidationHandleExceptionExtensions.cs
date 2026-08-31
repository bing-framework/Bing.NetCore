namespace Bing.Validation;

/// <summary>
/// 验证处理异常扩展
/// </summary>
public static class ValidationHandleExceptionExtensions
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="collection">验证结果集合</param>
    /// <returns>绑定指定验证结果集合的处理操作。</returns>
    public static ValidationHandleOperation Handle(this ValidationResultCollection collection) => new ValidationHandleOperation(collection);

    /// <summary>
    /// 处理所有验证
    /// </summary>
    /// <param name="op">验证处理操作</param>
    /// <param name="handler">验证处理器</param>
    /// <returns>已执行处理器的验证处理操作。</returns>
    public static ValidationHandleOperation HandleAll(this ValidationHandleOperation op, IValidationCallbackHandler handler)
    {
        if (op == null)
            throw new ArgumentNullException(nameof(op));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        op.Handle(handler);
        return op;
    }
}

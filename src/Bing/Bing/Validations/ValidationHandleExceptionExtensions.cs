using System;

namespace Bing.Validations
{
    /// <summary>
    /// 验证处理异常扩展
    /// </summary>
    public static class ValidationHandleExceptionExtensions
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="collection">验证结果集合</param>
        public static ValidationHandleOperation Handle(this ValidationResultCollection collection) => new ValidationHandleOperation(collection);

        /// <summary>
        /// 处理所有验证
        /// </summary>
        /// <param name="op">验证处理操作</param>
        /// <param name="handler">验证处理器</param>
        public static ValidationHandleOperation HandleAll(this ValidationHandleOperation op, Bing.Validations.Abstractions.IValidationHandler handler)
        {
            if (op == null)
                throw new ArgumentNullException(nameof(op));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            op.Handle(handler);
            return op;
        }
    }
}

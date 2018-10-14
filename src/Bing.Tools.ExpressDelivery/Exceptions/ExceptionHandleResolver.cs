using System;

namespace Bing.Tools.ExpressDelivery.Exceptions
{
    /// <summary>
    /// 异常处理解析器
    /// </summary>
    public static class ExceptionHandleResolver
    {
        /// <summary>
        /// 异常处理操作
        /// </summary>
        private static Action<Exception> _exceptionHandle { get; set; }

        /// <summary>
        /// 获取解析处理器
        /// </summary>
        /// <returns></returns>
        public static Action<Exception> ResolveHandler() => _exceptionHandle;

        /// <summary>
        /// 设置处理器
        /// </summary>
        /// <param name="handle">异常处理操作</param>
        public static void SetHandler(Action<Exception> handle) => _exceptionHandle += handle;

        /// <summary>
        /// 重置异常处理
        /// </summary>
        public static void Reset() => _exceptionHandle = null;
    }
}

using System;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Bing.Http;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 错误信息转换器
    /// </summary>
    public class DefaultExceptionToErrorInfoConverter : IExceptionToErrorInfoConverter, ITransientDependency
    {
        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="exception">异常</param>
        /// <param name="includeSensitiveDetails">是否包含敏感信息</param>
        public RemoteServiceErrorInfo Convert(Exception exception, bool includeSensitiveDetails)
        {
            var errorInfo = CreateErrorInfoWithoutCode(exception, includeSensitiveDetails);
            if (exception is IHasErrorCode hasErrorCodeException) 
                errorInfo.Code = hasErrorCodeException.Code;
            return errorInfo;
        }

        /// <summary>
        /// 创建错误信息（无错误码）
        /// </summary>
        /// <param name="exception">异常</param>
        /// <param name="includeSensitiveDetails">是否包含敏感信息</param>
        protected virtual RemoteServiceErrorInfo CreateErrorInfoWithoutCode(Exception exception, bool includeSensitiveDetails)
        {
            exception = TryToGetActualException(exception);

            var errorInfo = new RemoteServiceErrorInfo();
            errorInfo.Data = exception.Data;

            return errorInfo;
        }

        /// <summary>
        /// 尝试获取实际异常
        /// </summary>
        /// <param name="exception">异常</param>
        protected virtual Exception TryToGetActualException(Exception exception)
        {
            return exception;
        }
    }
}

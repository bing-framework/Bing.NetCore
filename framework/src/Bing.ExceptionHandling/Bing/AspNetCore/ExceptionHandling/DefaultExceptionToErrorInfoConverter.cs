using System.Text;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Bing.Http;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 错误信息转换器
/// </summary>
public class DefaultExceptionToErrorInfoConverter : IExceptionToErrorInfoConverter, ITransientDependency
{
    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="options">配置操作</param>
    public RemoteServiceErrorInfo Convert(Exception exception, Action<BingExceptionHandlingOptions> options = null)
    {
        var exceptionHandlingOptions = CreateDefaultOptions();
        options?.Invoke(exceptionHandlingOptions);

        var errorInfo = CreateErrorInfoWithoutCode(exception, exceptionHandlingOptions);

        if (exception is IHasErrorCode hasErrorCodeException)
            errorInfo.Code = hasErrorCodeException.Code;

        return errorInfo;
    }

    /// <summary>
    /// 创建错误消息，不包含错误码
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="options">异常处理选项配置</param>
    protected virtual RemoteServiceErrorInfo CreateErrorInfoWithoutCode(Exception exception, BingExceptionHandlingOptions options)
    {
        if (options.SendExceptionDetailsToClients)
        {
            return CreateDetailedErrorInfoFromException(exception, options.SendStackTraceToClients);
        }

        return default;
    }

    /// <summary>
    /// 从异常中创建错误信息【包含错误详情】
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="sendStackTraceToClients">是否发送异常详情到客户端</param>
    protected virtual RemoteServiceErrorInfo CreateDetailedErrorInfoFromException(Exception exception, bool sendStackTraceToClients)
    {
        var detailBuilder = new StringBuilder();

        AddExceptionToDetails(exception, detailBuilder, sendStackTraceToClients);

        var errorInfo = new RemoteServiceErrorInfo(exception.Message, detailBuilder.ToString(), data: exception.Data);

        return errorInfo;
    }

    /// <summary>
    /// 添加异常到详情
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="detailBuilder">异常详情字符串拼接器</param>
    /// <param name="sendStackTraceToClients">是否发送异常详情到客户端</param>
    protected virtual void AddExceptionToDetails(Exception exception, StringBuilder detailBuilder, bool sendStackTraceToClients)
    {
        // Exception Message
        detailBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);

        // Additional info for UserFriendlyException
        if (exception is IUserFriendlyException && exception is IHasErrorDetails)
        {
            var details = ((IHasErrorDetails)exception).Details;
            if (!string.IsNullOrWhiteSpace(details))
                detailBuilder.AppendLine(details);
        }

        // Additional info for BingValidationException

        // Exception StackTrace
        if (sendStackTraceToClients && !string.IsNullOrEmpty(exception.StackTrace))
            detailBuilder.AppendLine("STACK TRACE: " + exception.StackTrace);

        // Inner Exception
        if (exception.InnerException != null)
            AddExceptionToDetails(exception.InnerException, detailBuilder, sendStackTraceToClients);

        // Inner Exceptions for AggregateException
        if (exception is AggregateException aggregateException)
        {
            if (aggregateException.InnerExceptions.IsNullOrEmpty())
                return;
            foreach (var innerException in aggregateException.InnerExceptions)
                AddExceptionToDetails(innerException, detailBuilder, sendStackTraceToClients);
        }
    }


    /// <summary>
    /// 创建默认的异常处理选项配置
    /// </summary>
    protected virtual BingExceptionHandlingOptions CreateDefaultOptions()
    {
        return new BingExceptionHandlingOptions
        {
            SendExceptionDetailsToClients = false,
            SendStackTraceToClients = true,
        };
    }
}

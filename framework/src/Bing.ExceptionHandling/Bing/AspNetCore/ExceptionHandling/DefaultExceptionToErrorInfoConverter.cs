using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.ExceptionHandling;
using Bing.Exceptions;
using Bing.Http;
using Bing.Http.Clients;

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

        exception = TryToGetActualException(exception);

        // 远程调用异常
        if (exception is BingRemoteCallException remoteCallException && remoteCallException.Error != null)
            return remoteCallException.Error;

        // 并发异常
        if (exception is ConcurrencyException)
            return new RemoteServiceErrorInfo { Code = "400001", Message = exception.GetPrompt() };

        // 错误异常
        if (exception is Warning warning)
            return new RemoteServiceErrorInfo { Code = warning.Code, Message = warning.GetPrompt() };

        // 实体未找到异常
        if (exception is EntityNotFoundException entityNotFoundException)
            return CreateEntityNotFoundError(entityNotFoundException);

        var errorInfo = new RemoteServiceErrorInfo();

        // 用户友好提示异常
        if (exception is IUserFriendlyException || exception is BingRemoteCallException)
        {
            errorInfo.Message = exception.Message;
            errorInfo.Details = (exception as IHasErrorDetails)?.Details;
        }
        else
        {
            errorInfo.Message = exception.GetPrompt();
        }

        // 验证错误

        errorInfo.Data = exception.Data;

        return errorInfo;
    }

    /// <summary>
    /// 尝试从异常中获取实际的异常对象。
    /// </summary>
    /// <param name="exception">异常</param>
    protected virtual Exception TryToGetActualException(Exception exception)
    {
        if (exception is AggregateException aggException && aggException.InnerException != null)
        {
            if (aggException.InnerException is EntityNotFoundException ||
                aggException.InnerException is IBusinessException)
            {
                return aggException.InnerException;
            }
        }
        return exception;
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
    /// 创建实体未找到的错误信息
    /// </summary>
    /// <param name="exception">实体未找到异常</param>
    protected virtual RemoteServiceErrorInfo CreateEntityNotFoundError(EntityNotFoundException exception)
    {
        if (exception.EntityType != null)
            return new RemoteServiceErrorInfo(string.Format("不存在 id = {1} 的实体 {0}！", exception.EntityType.Name, exception.Id));
        return new RemoteServiceErrorInfo(exception.Message);
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

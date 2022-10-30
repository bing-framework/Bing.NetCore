﻿namespace Bing.ExceptionHandling;

/// <summary>
/// 异常订阅器基类
/// </summary>
public abstract class ExceptionSubscriber : IExceptionSubscriber
{
    /// <summary>
    /// 排序号。正序
    /// </summary>
    public virtual int Order => 10;

    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    public abstract Task HandleAsync(ExceptionNotificationContext context);
}

﻿using System.Runtime.Serialization;
using Bing.ExceptionHandling;
using Bing.Exceptions;

namespace Bing;

/// <summary>
/// Bing异常
/// </summary>
[Serializable]
public abstract class BingException : Exception, IHasErrorCode
{
    /// <summary>
    /// 空标识
    /// </summary>
    protected const string EMPTY_FLAG = "__EMPTY_FLG";

    /// <summary>
    /// 默认错误消息
    /// </summary>
    protected const string DEFAULT_ERROR_MESSAGE = "_DEFAULT_ERROR";

    /// <summary>
    /// 默认错误编码
    /// </summary>
    protected const long DEFAULT_ERROR_CODE = 1001;

    /// <summary>
    /// 默认扩展错误编码
    /// </summary>
    protected const long DEFAULT_EXTEND_ERROR_CODE = 1002;

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    protected BingException()
        : this(DEFAULT_ERROR_CODE, DEFAULT_ERROR_MESSAGE, EMPTY_FLAG)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="innerException">内部异常</param>
    protected BingException(string errorMessage, Exception innerException = null)
        : this(errorMessage, EMPTY_FLAG, innerException)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="flag">错误标识</param>
    /// <param name="innerException">内部异常</param>
    protected BingException(string errorMessage, string flag, Exception innerException = null)
        : this(DEFAULT_EXTEND_ERROR_CODE, errorMessage, flag, innerException)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="innerException">内部异常</param>
    protected BingException(long errorCode, string errorMessage, Exception innerException = null)
        : this(errorCode, errorMessage, EMPTY_FLAG, innerException)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="info">序列化信息</param>
    /// <param name="context">流上下文</param>
    protected BingException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        ExtraData = new Dictionary<string, object>();
        Code = DEFAULT_EXTEND_ERROR_CODE.ToString();
        Flag = EMPTY_FLAG;
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="flag">错误标识</param>
    /// <param name="innerException">内部异常</param>
    protected BingException(long errorCode, string errorMessage, string flag, Exception innerException = null) 
        : base(errorMessage, innerException)
    {
        if (string.IsNullOrWhiteSpace(flag))
            flag = EMPTY_FLAG;
        ExtraData = new Dictionary<string, object>();
        Code = errorCode.ToString();
        Flag = flag;
    }

    /// <summary>
    /// 初始化一个<see cref="BingException"/>类型的实例
    /// </summary>
    /// <param name="options">Bing框架异常选项配置</param>
    protected BingException(BingExceptionOptions options) 
        : base(options.Message, options.InnerException)
    {
        ExtraData = options.ExtraErrors;
        Code = options.ErrorCode.ToString();
        Flag = options.Flag;
    }

    /// <summary>
    /// 错误码
    /// </summary>
    public virtual string Code { get; protected set; }

    /// <summary>
    /// 错误标识
    /// </summary>
    public string Flag { get; protected set; }

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object> ExtraData { get; }

    /// <summary>
    /// 获取完整的消息
    /// </summary>
    public virtual string GetFullMessage() => $"{Code}:({Flag}){Message}";

    /// <summary>
    /// 抛出异常
    /// </summary>
    public virtual void Throw() => ExceptionHelper.PrepareForRethrow(this);
}

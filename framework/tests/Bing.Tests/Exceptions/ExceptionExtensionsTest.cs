using System;
using AspectCore.DynamicProxy;
using Bing.Exceptions;
using Bing.Exceptions.Prompts;
using Bing.Properties;
using Xunit;

namespace Bing.Tests.Exceptions;

/// <summary>
/// 异常扩展测试
/// </summary>
public class ExceptionExtensionsTest
{
    public ExceptionExtensionsTest()
    {
        ExceptionPrompt.AddPrompt(new AspectExceptionPrompt());
    }

    /// <summary>
    /// 测试 - 获取原始异常
    /// </summary>
    [Fact]
    public void Test_GetRawException()
    {
        var exception = new AspectInvocationException(null, new Exception("a"));
        Assert.Equal("a", exception.GetRawException().Message);
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 验证空
    /// </summary>
    [Fact]
    public void Test_GetPrompt_Null()
    {
        Exception exception = null;
        Assert.Null(exception.GetPrompt());
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 生产环境隐藏系统异常消息
    /// </summary>
    [Fact]
    public void Test_GetPrompt_IsProduction_True()
    {
        var exception = new Exception("a");
        Assert.Equal(R.SystemError, exception.GetPrompt(true));
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 非生产环境显示系统异常消息
    /// </summary>
    [Fact]
    public void Test_GetPrompt_IsProduction_False()
    {
        var exception = new Exception("a");
        Assert.Equal("a", exception.GetPrompt());
    }

    /// <summary>
    /// 测试 - 获取异常提示 - Warning异常生产环境显示正常
    /// </summary>
    [Fact]
    public void Test_GetPrompt_Warning_IsProduction_True()
    {
        var exception = new Warning("a");
        Assert.Equal("a", exception.GetPrompt(true));
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 非生产环境显示Warning异常消息
    /// </summary>
    [Fact]
    public void Test_GetPrompt_Warning_IsProduction_False()
    {
        var exception = new Warning("a");
        Assert.Equal("a", exception.GetPrompt());
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 显示Warning多层异常消息
    /// </summary>
    [Fact]
    public void Test_GetPrompt_Warning_2Level()
    {
        var rawException = new Exception("a");
        var exception = new Warning(rawException);
        Assert.Equal("a",exception.GetPrompt());
    }

    /// <summary>
    /// 测试 - 获取异常提示 - 显示原始异常消息
    /// </summary>
    [Fact]
    public void TestPrompt_AspectInvocationException()
    {
        var exception= new AspectInvocationException(null, new Exception("a"));
        Assert.Equal("a", exception.GetPrompt());
    }
}
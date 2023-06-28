using Bing.Exceptions;

namespace Bing.Tests.Exceptions;

/// <summary>
/// 应用程序异常测试
/// </summary>
public class WarningTest
{
    /// <summary>
    /// 测试 - 设置消息
    /// </summary>
    [Fact]
    public void Test_Message()
    {
        var warning = new Warning("A");
        Assert.Equal("A", warning.Message);
    }

    /// <summary>
    /// 测试 - 消息为空
    /// </summary>
    [Fact]
    public void Test_Message_Null()
    {
        var warning = new Warning(null, "A");
        Assert.Equal(string.Empty, warning.Message);
    }

    /// <summary>
    /// 测试 - 设置错误码
    /// </summary>
    [Fact]
    public void Test_Code()
    {
        var warning = new Warning("", "B");
        Assert.Equal("B", warning.Code);
    }

    /// <summary>
    /// 测试 - 设置异常
    /// </summary>
    [Fact]
    public void Test_Exception()
    {
        var warning = new Warning(new Exception("A"));
        Assert.Empty(warning.Message);
        Assert.Equal("A", warning.GetMessage());
    }

    /// <summary>
    /// 测试 - 设置错误消息和异常
    /// </summary>
    [Fact]
    public void Test_MessageAndException()
    {
        var warning = new Warning("A", "", new Exception("C"));
        Assert.Equal("A", warning.Message);
        Assert.Equal($"A{Environment.NewLine}C", warning.GetMessage());
    }

    /// <summary>
    /// 测试 - 设置2层异常
    /// </summary>
    [Fact]
    public void Test_Exception_2Layer()
    {
        var warning = new Warning("A", "", new Exception("C", new NotImplementedException("D")));
        Assert.Equal(3, warning.GetExceptions().Count);
        Assert.Equal(typeof(Warning), warning.GetExceptions()[0].GetType());
        Assert.Equal(typeof(Exception), warning.GetExceptions()[1].GetType());
        Assert.Equal(typeof(NotImplementedException), warning.GetExceptions()[2].GetType());
        Assert.Equal("A", warning.GetExceptions()[0].Message);
        Assert.Equal("C", warning.GetExceptions()[1].Message);
        Assert.Equal("D", warning.GetExceptions()[2].Message);
        Assert.Equal($"A{Environment.NewLine}C{Environment.NewLine}D", warning.GetMessage());
    }

    /// <summary>
    /// 测试 - 获取异常列表
    /// </summary>
    [Fact]
    public void Test_GetExceptions_1()
    {
        var exception = new Exception("A");
        var list = Warning.GetExceptions(exception);
        Assert.Single(list);
        Assert.Equal("A", list[0].Message);
    }

    /// <summary>
    /// 测试 - 获取异常列表
    /// </summary>
    [Fact]
    public void Test_GetExceptions_2()
    {
        var exceptionB = new Exception("B");
        var exceptionA = new Exception("A", exceptionB);
        var list = Warning.GetExceptions(exceptionA);
        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].Message);
        Assert.Equal("B", list[1].Message);
    }
}

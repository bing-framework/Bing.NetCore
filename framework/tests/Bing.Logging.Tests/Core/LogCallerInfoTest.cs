using Bing.Logging.Core.Callers;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LogCallerInfo"/> 及 <see cref="NullLogCallerInfo"/> 单元测试
/// </summary>
public class LogCallerInfoTest
{
    // ═══════════════════════════════════════════════════════════
    // LogCallerInfo 构造与属性
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用三个参数构造时，MemberName/FilePath/LineNumber 应被正确保存。
    /// </summary>
    [Fact]
    public void LogCallerInfo_FullCtor_ShouldSetAllProperties()
    {
        // Arrange & Act
        var info = new LogCallerInfo("MyMethod", "/src/MyFile.cs", 42);

        // Assert
        info.MemberName.ShouldBe("MyMethod");
        info.FilePath.ShouldBe("/src/MyFile.cs");
        info.LineNumber.ShouldBe(42);
    }

    /// <summary>
    /// 测试目的：仅提供 memberName 时，FilePath 默认 null，LineNumber 默认 0。
    /// </summary>
    [Fact]
    public void LogCallerInfo_OnlyMemberName_DefaultsForOthers()
    {
        // Arrange & Act
        var info = new LogCallerInfo("SomeMethod");

        // Assert
        info.MemberName.ShouldBe("SomeMethod");
        info.FilePath.ShouldBeNull();
        info.LineNumber.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：ToParams() 返回的动态对象应包含三个属性，值与构造参数一致。
    /// </summary>
    [Fact]
    public void LogCallerInfo_ToParams_ShouldExposeAllFields()
    {
        // Arrange
        var info = new LogCallerInfo("Execute", "/app/Service.cs", 99);

        // Act
        dynamic p = info.ToParams();

        // Assert
        ((string)p.MemberName).ShouldBe("Execute");
        ((string)p.FilePath).ShouldBe("/app/Service.cs");
        ((int)p.LineNumber).ShouldBe(99);
    }

    // ═══════════════════════════════════════════════════════════
    // NullLogCallerInfo 空实现
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：NullLogCallerInfo.Instance 是单例，所有属性均为默认值（null/0）。
    /// </summary>
    [Fact]
    public void NullLogCallerInfo_Instance_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var info = NullLogCallerInfo.Instance;

        // Assert
        info.MemberName.ShouldBeNull();
        info.FilePath.ShouldBeNull();
        info.LineNumber.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：NullLogCallerInfo.ToParams() 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void NullLogCallerInfo_ToParams_ShouldReturnNull()
    {
        // Arrange & Act
        var result = NullLogCallerInfo.Instance.ToParams();

        // Assert
        ((object)result).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：NullLogCallerInfo 应实现 ILogCallerInfo 接口，可被多态使用。
    /// </summary>
    [Fact]
    public void NullLogCallerInfo_ShouldImplementILogCallerInfo()
    {
        // Arrange & Act
        ILogCallerInfo info = NullLogCallerInfo.Instance;

        // Assert
        info.ShouldNotBeNull();
        info.ShouldBeAssignableTo<ILogCallerInfo>();
    }
}

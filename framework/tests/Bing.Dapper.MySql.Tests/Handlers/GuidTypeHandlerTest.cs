using System.Data;
using Dapper.Handlers;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Handlers;

/// <summary>
/// <see cref="GuidTypeHandler"/> 单元测试
/// 覆盖 MySQL 特有的 Guid 字节序转换逻辑（Data1/Data2/Data3 大端序存储）。
/// </summary>
public class GuidTypeHandlerTest
{
    private readonly GuidTypeHandler _handler = new();

    // ═══════════════════════════════════════════════════════════
    // Parse
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Parse(null) 应返回 Guid.Empty，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void Parse_WhenNull_ShouldReturnGuidEmpty()
    {
        // Act
        var result = _handler.Parse(null!);

        // Assert
        result.ShouldBe(Guid.Empty);
    }

    /// <summary>
    /// 测试目的：Parse 对经过字节序转换的字节数组，应能还原出原始 Guid（双向转换幂等）。
    /// </summary>
    [Fact]
    public void Parse_AfterSetValue_ShouldRoundTripCorrectly()
    {
        // Arrange
        var original = Guid.NewGuid();
        var mockParam = new Mock<IDbDataParameter>();
        byte[]? capturedBytes = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => capturedBytes = (byte[])v);

        // Act
        _handler.SetValue(mockParam.Object, original);
        var restored = _handler.Parse(capturedBytes!);

        // Assert
        restored.ShouldBe(original);
    }

    /// <summary>
    /// 测试目的：Parse 对全零字节（对应 Guid.Empty 的存储格式），应还原为 Guid.Empty。
    /// </summary>
    [Fact]
    public void Parse_AllZeroBytes_ShouldReturnGuidEmpty()
    {
        // Arrange
        var zeroBytes = new byte[16];

        // Act
        var result = _handler.Parse(zeroBytes);

        // Assert
        result.ShouldBe(Guid.Empty);
    }

    /// <summary>
    /// 测试目的：Parse 处理的字节数组长度应为 16，与标准 Guid 字节数一致。
    /// </summary>
    [Fact]
    public void Parse_ResultShouldHave16ByteRepresentation()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var mockParam = new Mock<IDbDataParameter>();
        byte[]? capturedBytes = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => capturedBytes = (byte[])v);
        _handler.SetValue(mockParam.Object, guid);

        // Act
        var restored = _handler.Parse(capturedBytes!);

        // Assert
        restored.ToByteArray().Length.ShouldBe(16);
    }

    // ═══════════════════════════════════════════════════════════
    // SetValue
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SetValue(null, guid) 应静默忽略，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void SetValue_WhenParameterIsNull_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _handler.SetValue(null!, Guid.NewGuid()));
    }

    /// <summary>
    /// 测试目的：SetValue(parameter, Guid.Empty) 应静默忽略，不写入 parameter.Value。
    /// </summary>
    [Fact]
    public void SetValue_WhenGuidIsEmpty_ShouldNotSetParameterValue()
    {
        // Arrange
        var mockParam = new Mock<IDbDataParameter>();

        // Act
        _handler.SetValue(mockParam.Object, Guid.Empty);

        // Assert
        mockParam.VerifySet(p => p.Value = It.IsAny<object>(), Times.Never);
    }

    /// <summary>
    /// 测试目的：SetValue 对有效 Guid 应将字节序转换后的 16 字节数组写入 parameter.Value。
    /// </summary>
    [Fact]
    public void SetValue_WithValidGuid_ShouldSetParameterValueToByteArray()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var mockParam = new Mock<IDbDataParameter>();
        object? assignedValue = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => assignedValue = v);

        // Act
        _handler.SetValue(mockParam.Object, guid);

        // Assert
        assignedValue.ShouldNotBeNull();
        var bytes = assignedValue as byte[];
        bytes.ShouldNotBeNull();
        bytes!.Length.ShouldBe(16);
    }

    /// <summary>
    /// 测试目的：SetValue 写入的字节数组应与原始 Guid 字节数组不同（因为字节序已被调换）。
    /// </summary>
    [Fact]
    public void SetValue_ShouldProduceByteSwappedArray_DifferentFromOriginal()
    {
        // Arrange
        // 使用一个各 byte 明显不同的 Guid（避免对称情况导致 false positive）
        var guid = new Guid("01020304-0506-0708-090a-0b0c0d0e0f10");
        var originalBytes = guid.ToByteArray();
        var mockParam = new Mock<IDbDataParameter>();
        byte[]? stored = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => stored = (byte[])v);

        // Act
        _handler.SetValue(mockParam.Object, guid);

        // Assert
        stored.ShouldNotBeNull();
        // Data1 字节序被颠倒（bytes 0-3）
        stored![0].ShouldBe(originalBytes[3]);
        stored[1].ShouldBe(originalBytes[2]);
        stored[2].ShouldBe(originalBytes[1]);
        stored[3].ShouldBe(originalBytes[0]);
        // Data2 字节序被颠倒（bytes 4-5）
        stored[4].ShouldBe(originalBytes[5]);
        stored[5].ShouldBe(originalBytes[4]);
        // Data3 字节序被颠倒（bytes 6-7）
        stored[6].ShouldBe(originalBytes[7]);
        stored[7].ShouldBe(originalBytes[6]);
        // 后 8 字节保持不变
        for (int i = 8; i < 16; i++)
            stored[i].ShouldBe(originalBytes[i]);
    }

    /// <summary>
    /// 测试目的：多个不同 Guid 的 SetValue→Parse 均应还原为原始值（幂等性批量验证）。
    /// </summary>
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("12345678-1234-5678-1234-567812345678")]
    public void Parse_RoundTrip_ShouldAlwaysRestoreOriginalGuid(string guidStr)
    {
        // Arrange
        var original = Guid.Parse(guidStr);
        var mockParam = new Mock<IDbDataParameter>();
        byte[]? stored = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => stored = (byte[])v);

        // Act
        _handler.SetValue(mockParam.Object, original);
        var restored = _handler.Parse(stored!);

        // Assert
        restored.ShouldBe(original);
    }
}

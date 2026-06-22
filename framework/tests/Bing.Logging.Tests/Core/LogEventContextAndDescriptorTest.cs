using Bing.Logging.Core;
using Bing.Logging.Core.Callers;
using Bing.Logging.ExtraSupports;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests.Core;

/// <summary>
/// <see cref="LogEventContext"/> 及 <see cref="LogEventDescriptor"/> 单元测试
/// </summary>
public class LogEventContextAndDescriptorTest
{
    // ═══════════════════════════════════════════════════════════
    // LogEventContext - SetTags（通过 ExposeScopeState 验证，因为 Tags 是 internal）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：未设置标签时 ExposeScopeState 不应包含 Tags 键。
    /// </summary>
    [Fact]
    public void LogEventContext_NoTags_ExposeScopeStateShouldNotContainTagsKey()
    {
        // Arrange & Act
        var ctx = new LogEventContext();

        // Assert
        var state = ctx.ExposeScopeState();
        state.ContainsKey(ContextDataTypes.Tags).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：SetTags 后 ExposeScopeState 应包含 Tags 键，空白标签被忽略（不产生键）。
    /// </summary>
    [Fact]
    public void SetTags_WithValidAndBlankTags_ExposeScopeStateShouldContainTagsKey()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetTags("tag-a", "", "  ", "tag-b");
        var state = ctx.ExposeScopeState();

        // Assert
        state.ShouldContainKey(ContextDataTypes.Tags);
    }

    /// <summary>
    /// 测试目的：只传入空白标签时，ExposeScopeState 不应包含 Tags 键。
    /// </summary>
    [Fact]
    public void SetTags_OnlyBlankTags_ExposeScopeStateShouldNotHaveTagsKey()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetTags("", "  ");
        var state = ctx.ExposeScopeState();

        // Assert
        state.ContainsKey(ContextDataTypes.Tags).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：SetTags(null) 不抛异常，ExposeScopeState 中也不含 Tags 键。
    /// </summary>
    [Fact]
    public void SetTags_WithNull_ShouldNotThrowAndNoTagsKey()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act & Assert
        Should.NotThrow(() => ctx.SetTags(null));
        ctx.ExposeScopeState().ContainsKey(ContextDataTypes.Tags).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：SetTags 应返回 this，支持链式调用。
    /// </summary>
    [Fact]
    public void SetTags_ShouldReturnSelf()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        var result = ctx.SetTags("t1");

        // Assert
        ReferenceEquals(result, ctx).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // LogEventContext - SetParameter / SetParameters
    // （Parameters 是 internal，只验证调用链式返回与不抛异常）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SetParameter 传入有效对象不抛异常，并返回 this。
    /// </summary>
    [Fact]
    public void SetParameter_WithValue_ShouldReturnSelf()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        var result = ctx.SetParameter(new { userId = "u-001" });

        // Assert
        Should.NotThrow(() => { });
        ReferenceEquals(result, ctx).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：SetParameter(null) 不抛异常，返回 this。
    /// </summary>
    [Fact]
    public void SetParameter_WithNull_ShouldReturnSelfWithoutThrow()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act & Assert
        LogEventContext result = null;
        Should.NotThrow(() => result = ctx.SetParameter(null));
        ReferenceEquals(result, ctx).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：SetParameters 批量调用不抛异常，返回 this。
    /// </summary>
    [Fact]
    public void SetParameters_WithMultiple_ShouldReturnSelf()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        var result = ctx.SetParameters("p1", 2, true);

        // Assert
        ReferenceEquals(result, ctx).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // LogEventContext - ExtraProperties
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SetExtraProperty 后 ExtraProperties 应包含以 ExtraProperty 前缀开头的键。
    /// </summary>
    [Fact]
    public void SetExtraProperty_WithValidNameAndValue_ShouldBeStored()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetExtraProperty("requestId", "req-001");

        // Assert
        ctx.ExtraProperties.ShouldNotBeNull();
        ctx.ExtraProperties.ContainsKey($"{ContextDataTypes.ExtraProperty}requestId").ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：SetExtraProperty(name=null 或空白) 应被忽略，ExtraProperties 保持为空。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void SetExtraProperty_BlankName_ShouldBeIgnored(string name)
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetExtraProperty(name, "value");

        // Assert
        ctx.ExtraProperties.Any().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：SetExtraProperty(value=null) 应被忽略，ExtraProperties 保持为空。
    /// </summary>
    [Fact]
    public void SetExtraProperty_NullValue_ShouldBeIgnored()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetExtraProperty("key", null);

        // Assert
        ctx.ExtraProperties.Any().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：SetExtraProperty 后 ExposeScopeState 中应包含对应的扩展属性键。
    /// </summary>
    [Fact]
    public void SetExtraProperty_ExposeScopeStateShouldContainKey()
    {
        // Arrange
        var ctx = new LogEventContext();
        ctx.SetExtraProperty("correlationId", "corr-abc");

        // Act
        var state = ctx.ExposeScopeState();

        // Assert
        state.ShouldContainKey($"{ContextDataTypes.ExtraProperty}correlationId");
    }

    // ═══════════════════════════════════════════════════════════
    // LogEventContext - CallerInfo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认 LogCallerInfo 应为 NullLogCallerInfo（未设置时）。
    /// </summary>
    [Fact]
    public void LogEventContext_Default_CallerInfoShouldBeNullLogCallerInfo()
    {
        // Arrange & Act
        var ctx = new LogEventContext();

        // Assert
        ctx.LogCallerInfo.ShouldBeOfType<NullLogCallerInfo>();
    }

    /// <summary>
    /// 测试目的：SetCallerInfo 后 LogCallerInfo 应为 LogCallerInfo 类型，包含传入的成员名与行号。
    /// </summary>
    [Fact]
    public void SetCallerInfo_WithMemberName_ShouldSetLogCallerInfo()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetCallerInfo("ProcessOrder", "/app/OrderService.cs", 55);

        // Assert
        ctx.LogCallerInfo.ShouldBeOfType<LogCallerInfo>();
        ctx.LogCallerInfo.MemberName.ShouldBe("ProcessOrder");
        ctx.LogCallerInfo.LineNumber.ShouldBe(55);
    }

    /// <summary>
    /// 测试目的：SetCallerInfo 所有参数均为默认（空/0）时不应覆盖 NullLogCallerInfo。
    /// </summary>
    [Fact]
    public void SetCallerInfo_AllDefaults_ShouldKeepNullLogCallerInfo()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        ctx.SetCallerInfo();

        // Assert
        ctx.LogCallerInfo.ShouldBeOfType<NullLogCallerInfo>();
    }

    /// <summary>
    /// 测试目的：SetCallerInfo 设置后 ExposeScopeState 应包含 CallerInfo 键。
    /// </summary>
    [Fact]
    public void SetCallerInfo_ExposeScopeStateShouldContainCallerInfoKey()
    {
        // Arrange
        var ctx = new LogEventContext();
        ctx.SetCallerInfo("Run", "/app/Runner.cs", 10);

        // Act
        var state = ctx.ExposeScopeState();

        // Assert
        state.ShouldContainKey(ContextDataTypes.CallerInfo);
    }

    // ═══════════════════════════════════════════════════════════
    // LogEventContext - ExposeScopeState
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：设置标签后 ExposeScopeState 应包含 Tags 键。
    /// </summary>
    [Fact]
    public void ExposeScopeState_WithTags_ShouldContainTagsKey()
    {
        // Arrange
        var ctx = new LogEventContext();
        ctx.SetTags("order", "payment");

        // Act
        var state = ctx.ExposeScopeState();

        // Assert
        state.ShouldContainKey(ContextDataTypes.Tags);
    }

    /// <summary>
    /// 测试目的：无任何数据时 ExposeScopeState 应返回空字典。
    /// </summary>
    [Fact]
    public void ExposeScopeState_WithNoData_ShouldReturnEmpty()
    {
        // Arrange
        var ctx = new LogEventContext();

        // Act
        var state = ctx.ExposeScopeState();

        // Assert
        state.Count.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════
    // LogEventDescriptor
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Context 不为 null，保证使用时无空引用异常。
    /// </summary>
    [Fact]
    public void LogEventDescriptor_Default_ContextShouldNotBeNull()
    {
        // Arrange & Act
        var descriptor = new LogEventDescriptor();

        // Assert
        descriptor.Context.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：TraceId/TraceName/BusinessTraceId 属性可读写，用于全链路追踪。
    /// </summary>
    [Fact]
    public void LogEventDescriptor_TraceProperties_ShouldBeReadWritable()
    {
        // Arrange
        var descriptor = new LogEventDescriptor();

        // Act
        descriptor.TraceId = "trace-abc";
        descriptor.TraceName = "OrderFlow";
        descriptor.BusinessTraceId = "biz-001";

        // Assert
        descriptor.TraceId.ShouldBe("trace-abc");
        descriptor.TraceName.ShouldBe("OrderFlow");
        descriptor.BusinessTraceId.ShouldBe("biz-001");
    }

    /// <summary>
    /// 测试目的：默认构造后 TraceId/TraceName/BusinessTraceId 均为 null。
    /// </summary>
    [Fact]
    public void LogEventDescriptor_Default_AllTraceFieldsShouldBeNull()
    {
        // Arrange & Act
        var descriptor = new LogEventDescriptor();

        // Assert
        descriptor.TraceId.ShouldBeNull();
        descriptor.TraceName.ShouldBeNull();
        descriptor.BusinessTraceId.ShouldBeNull();
    }
}

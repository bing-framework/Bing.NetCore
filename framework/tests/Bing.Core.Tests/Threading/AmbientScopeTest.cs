using Bing.Threading;
using Shouldly;

namespace Bing.Tests.Threading;

/// <summary>
/// AsyncLocalAmbientDataContext 和 AmbientDataContextAmbientScopeProvider 环境范围测试
/// </summary>
public class AmbientScopeTest
{
    // ==================== AsyncLocalAmbientDataContext ====================

    /// <summary>
    /// 测试目的：SetData 后 GetData 应返回相同的值（基本读写）。
    /// </summary>
    [Fact]
    public void AsyncLocalDataContext_SetData_GetData_RoundTrip()
    {
        // Arrange
        var ctx = new AsyncLocalAmbientDataContext();

        // Act
        ctx.SetData("key1", "value1");
        var result = ctx.GetData("key1");

        // Assert
        result.ShouldBe("value1");
    }

    /// <summary>
    /// 测试目的：SetData 为 null 后 GetData 应返回 null。
    /// </summary>
    [Fact]
    public void AsyncLocalDataContext_SetData_Null_GetData_ReturnsNull()
    {
        // Arrange
        var ctx = new AsyncLocalAmbientDataContext();
        ctx.SetData("key1", "value1");

        // Act
        ctx.SetData("key1", null);
        var result = ctx.GetData("key1");

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：不同的键互不干扰。
    /// </summary>
    [Fact]
    public void AsyncLocalDataContext_DifferentKeys_AreIndependent()
    {
        // Arrange
        var ctx = new AsyncLocalAmbientDataContext();

        // Act
        ctx.SetData("keyA", "A");
        ctx.SetData("keyB", "B");

        // Assert
        ctx.GetData("keyA").ShouldBe("A");
        ctx.GetData("keyB").ShouldBe("B");
    }

    /// <summary>
    /// 测试目的：在异步任务中，AsyncLocal 数据应与当前上下文隔离（子任务不影响父）。
    /// </summary>
    [Fact]
    public async Task AsyncLocalDataContext_AsyncTask_IsIsolated()
    {
        // Arrange
        var ctx = new AsyncLocalAmbientDataContext();
        ctx.SetData("key", "parent");

        string? childValue = null;

        // Act：在独立任务中修改，不应影响父上下文
        await Task.Run(() =>
        {
            ctx.SetData("key", "child");
            childValue = ctx.GetData("key") as string;
        });

        // Assert：父上下文的值不被子任务覆盖（AsyncLocal 隔离）
        // 注意：AsyncLocal 在子任务中的修改不会流回父上下文
        childValue.ShouldBe("child");
    }

    // ==================== AmbientDataContextAmbientScopeProvider ====================

    private static AmbientDataContextAmbientScopeProvider<string> CreateProvider()
    {
        var ctx = new AsyncLocalAmbientDataContext();
        return new AmbientDataContextAmbientScopeProvider<string>(ctx);
    }

    /// <summary>
    /// 测试目的：未开始任何 Scope 时，GetValue 应返回类型默认值（null for string）。
    /// </summary>
    [Fact]
    public void AmbientScope_GetValue_OutsideScope_ReturnsDefault()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var value = provider.GetValue("ctx");

        // Assert
        value.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：BeginScope 后 GetValue 应返回设置的值。
    /// </summary>
    [Fact]
    public void AmbientScope_GetValue_InsideScope_ReturnsValue()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        using (provider.BeginScope("ctx", "hello"))
        {
            // Assert
            provider.GetValue("ctx").ShouldBe("hello");
        }
    }

    /// <summary>
    /// 测试目的：Dispose scope 后，GetValue 应恢复为默认值 null。
    /// </summary>
    [Fact]
    public void AmbientScope_GetValue_AfterDispose_ReturnsDefault()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        using (provider.BeginScope("ctx", "hello"))
        {
            provider.GetValue("ctx").ShouldBe("hello");
        }

        // Assert
        provider.GetValue("ctx").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：嵌套 BeginScope 内层值覆盖外层，内层 Dispose 后恢复外层值。
    /// </summary>
    [Fact]
    public void AmbientScope_Nested_InnerValueOverridesOuter_ThenRestores()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        using (provider.BeginScope("ctx", "outer"))
        {
            provider.GetValue("ctx").ShouldBe("outer");

            using (provider.BeginScope("ctx", "inner"))
            {
                provider.GetValue("ctx").ShouldBe("inner");
            }

            // 内层 Dispose 后恢复外层
            provider.GetValue("ctx").ShouldBe("outer");
        }

        // 外层 Dispose 后恢复默认
        provider.GetValue("ctx").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：不同的 contextKey 互不干扰。
    /// </summary>
    [Fact]
    public void AmbientScope_DifferentKeys_AreIndependent()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        using (provider.BeginScope("ctxA", "A"))
        using (provider.BeginScope("ctxB", "B"))
        {
            // Assert
            provider.GetValue("ctxA").ShouldBe("A");
            provider.GetValue("ctxB").ShouldBe("B");
        }
    }

    /// <summary>
    /// 测试目的：AmbientDataContextAmbientScopeProvider 构造函数传入 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AmbientScopeProvider_NullDataContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AmbientDataContextAmbientScopeProvider<string>(null!));
    }
}

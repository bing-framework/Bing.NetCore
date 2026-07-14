using Bing.Tracing;

namespace Bing.Tests.CorrelationIdProvider;

public class CorrelationIdProviderTest
{
    /// <summary>
    /// 测试目的：更改关联标识后应在作用域释放时恢复原值。
    /// </summary>
    [Fact]
    public void Test()
    {
        var correlationIdProvider = new DefaultCorrelationIdProvider();

        correlationIdProvider.Get().ShouldBeNull();

        var correlationId = Guid.NewGuid().ToString("N");
        using (correlationIdProvider.Change(correlationId))
        {
            correlationIdProvider.Get().ShouldBe(correlationId);
        }

        correlationIdProvider.Get().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：嵌套关联标识作用域释放后应逐级恢复父级标识。
    /// </summary>
    [Fact]
    public void Change_WhenNested_ShouldRestoreParentCorrelationId()
    {
        // Arrange
        var provider = new DefaultCorrelationIdProvider();

        // Act & Assert
        using (provider.Change("parent"))
        {
            provider.Get().ShouldBe("parent");
            using (provider.Change("child"))
                provider.Get().ShouldBe("child");
            provider.Get().ShouldBe("parent");
        }
        provider.Get().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：作用域内发生异常时，释放作用域仍应恢复父级关联标识。
    /// </summary>
    [Fact]
    public void Change_WhenExceptionThrown_ShouldRestoreParentCorrelationId()
    {
        // Arrange
        var provider = new DefaultCorrelationIdProvider();

        // Act
        using (provider.Change("parent"))
        {
            Should.Throw<InvalidOperationException>(() =>
            {
                using (provider.Change("child"))
                    throw new InvalidOperationException();
            });

            // Assert
            provider.Get().ShouldBe("parent");
        }
        provider.Get().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：并行异步流程应各自保持关联标识，不能发生上下文串扰。
    /// </summary>
    [Fact]
    public async Task Change_WhenParallelAsyncFlows_ShouldKeepCorrelationIdsIsolated()
    {
        // Arrange
        var provider = new DefaultCorrelationIdProvider();
        var barrier = new Barrier(2);

        // Act
        var first = Task.Run(() => ExecuteAsync("first"));
        var second = Task.Run(() => ExecuteAsync("second"));
        var values = await Task.WhenAll(first, second);

        // Assert
        values.ShouldBe(new[] { "first", "second" });
        provider.Get().ShouldBeNull();

        async Task<string> ExecuteAsync(string correlationId)
        {
            using (provider.Change(correlationId))
            {
                barrier.SignalAndWait();
                await Task.Yield();
                return provider.Get();
            }
        }
    }
}

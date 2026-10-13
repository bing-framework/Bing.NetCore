using Bing.Tracing;

namespace Bing.Tests.CorrelationIdProvider;

/// <summary>
/// 默认关联标识提供程序测试。
/// </summary>
public class CorrelationIdProviderTest
{
    /// <summary>
    /// 验证关联标识在变更作用域结束后恢复为空。
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
}

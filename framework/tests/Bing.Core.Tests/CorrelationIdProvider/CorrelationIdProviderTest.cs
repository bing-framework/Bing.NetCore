using Bing.Tracing;

namespace Bing.Tests.CorrelationIdProvider;

public class CorrelationIdProviderTest
{
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

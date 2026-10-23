using System.Threading.Channels;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Background;

/// <summary>
/// 仅在内存队列中传递场景名称与操作标识。
/// </summary>
public sealed class ScenarioWorkQueue
{
    private readonly Channel<ScenarioWorkItem> _channel = Channel.CreateUnbounded<ScenarioWorkItem>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    public ChannelReader<ScenarioWorkItem> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(ScenarioWorkItem item, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(item, cancellationToken);
}

public sealed record ScenarioWorkItem(string OperationId, string Scenario)
{
    public OperationContextSnapshot? Context { get; init; }
}

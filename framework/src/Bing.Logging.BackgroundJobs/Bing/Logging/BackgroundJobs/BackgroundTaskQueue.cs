using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 有界后台任务队列
/// </summary>
internal sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<WorkItem> _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundTaskQueue(
        IOptions<BackgroundTaskQueueOptions> options,
        IServiceScopeFactory scopeFactory)
    {
        var capacity = options?.Value?.Capacity ?? throw new ArgumentNullException(nameof(options));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "队列容量必须大于0。");
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public async ValueTask QueueAsync(BackgroundTaskDelegate task, CancellationToken cancellationToken = default)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        LogContextSnapshot snapshot;
        using (var scope = _scopeFactory.CreateScope())
            snapshot = scope.ServiceProvider.GetRequiredService<ILogContextAccessor>().Capture();
        await _queue.Writer.WriteAsync(new WorkItem(task, snapshot), cancellationToken);
    }

    public ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAsync(cancellationToken);

    internal sealed class WorkItem
    {
        public WorkItem(BackgroundTaskDelegate task, LogContextSnapshot context)
        {
            Task = task;
            Context = context;
        }

        public BackgroundTaskDelegate Task { get; }
        public LogContextSnapshot Context { get; }
    }
}
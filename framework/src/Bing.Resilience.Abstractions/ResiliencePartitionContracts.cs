namespace Bing.Resilience;

/// <summary>动态分区租约：使用期间保留状态，释放后进入有界冷却。</summary>
public interface IResiliencePartitionLease<T> : IDisposable
{
    IResiliencePipeline<T> Pipeline { get; }
}
public interface IResiliencePartitionedPipelineProvider
{
    IResiliencePartitionLease<T> AcquirePartition<T>(string name, string purpose, string partition,
        int maximumPartitions = 128, TimeSpan? retention = null);
    int CollectPartitions();
    int PartitionCount { get; }
}

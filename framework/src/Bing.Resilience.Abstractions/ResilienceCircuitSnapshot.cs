namespace Bing.Resilience;

/// <summary>共享熔断器的详细快照。配置版本在同一宿主的代切换中保持稳定。</summary>
public sealed record ResilienceCircuitSnapshot(ResiliencePipelineIdentity Identity, ResilienceCircuitState State,
    DateTimeOffset ObservedAt, DateTimeOffset? NextProbeAt, long Epoch, string ConfigurationVersion,
    int Samples, int Failures);
public interface IResilienceCircuitStateReader
{
    ResilienceCircuitSnapshot GetCircuitSnapshot(ResiliencePipelineIdentity identity);
}

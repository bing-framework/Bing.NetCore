namespace Bing.Resilience;

/// <summary>Polly 可选适配入口，公共签名只使用中性类型。</summary>
public static class PollyResilienceExtensions
{
    public static BingResilienceBuilder AddPollyProvider(this BingResilienceBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddProvider(new Internal.Polly.EngineProvider(name));
    }
}

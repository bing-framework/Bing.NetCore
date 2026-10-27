namespace Bing.Resilience;

/// <summary>Fences 可选适配入口，公共签名只使用中性类型。</summary>
public static class FencesResilienceExtensions
{
    public static BingResilienceBuilder AddFencesProvider(this BingResilienceBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddProvider(new Internal.Fences.EngineProvider(name));
    }
}

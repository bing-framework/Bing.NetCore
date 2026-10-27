using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class ResilienceBoundaryTests
{
    [Fact]
    public void FirstResolutionRejectsMissingProviderEvenBeforeExecuting()
    {
        var services = new ServiceCollection();
        services.AddBingResilience();
        services.AddBingResiliencePipeline<int>("unprotected", _ => { });
        using var provider = services.BuildServiceProvider();
        Assert.Throws<ResilienceConfigurationException>(() => provider.GetRequiredService<IResiliencePipelineProvider>());
    }

    [Fact]
    public void PublicContractsAndCoreDependenciesAreEngineNeutral()
    {
        var assemblies = new[] { typeof(IResiliencePipeline<>).Assembly, typeof(BingResilienceBuilder).Assembly, typeof(BingHttpResilienceOptions).Assembly };
        foreach (var assembly in assemblies)
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => Forbidden(a.Name!));
            foreach (var type in assembly.GetExportedTypes())
            {
                Inspect(type.BaseType);
                foreach (var contract in type.GetInterfaces()) Inspect(contract);
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (member is PropertyInfo property) Inspect(property.PropertyType);
                    if (member is FieldInfo field) Inspect(field.FieldType);
                    if (member is EventInfo signal) Inspect(signal.EventHandlerType);
                    if (member is MethodInfo method) Inspect(method.ReturnType);
                    if (member is MethodBase callable)
                        foreach (var parameter in callable.GetParameters()) Inspect(parameter.ParameterType);
                }
            }
        }
        static bool Forbidden(string name) => name.StartsWith("Polly", StringComparison.Ordinal) ||
            name.StartsWith("Paramore.Fences", StringComparison.Ordinal) ||
            name is "Microsoft.Extensions.Http.Resilience" or "Microsoft.Extensions.Resilience" or "Bing.Core";
        static void Inspect(Type? type)
        {
            if (type is null) return;
            Assert.False(Forbidden(type.Assembly.GetName().Name!), type.FullName);
            if (type.HasElementType) Inspect(type.GetElementType());
            if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Inspect(argument);
        }
    }

    private readonly record struct MutableContainer(List<int> Items);

    [Fact]
    public void ContextRejectsStructContainingMutableReferencesWithoutCopier()
    {
        var context = new ResilienceExecutionContext();
        context.Set(new ResiliencePropertyKey<MutableContainer>("mutable"), new(new List<int> { 1 }));
        Assert.Throws<ResilienceConfigurationException>(() => context.Fork(default));
        var copied = new ResilienceExecutionContext();
        var key = new ResiliencePropertyKey<MutableContainer>("copied", x => new(x.Items.ToList()));
        copied.Set(key, new(new List<int> { 1 }));
        var branch = copied.Fork(default);
        Assert.True(branch.TryGet(key, out var value)); value.Items.Add(2);
        Assert.True(copied.TryGet(key, out var original)); Assert.Single(original.Items);
    }

    [Fact]
    public async Task HttpRejectsStructContainingMutableReferencesBeforeSending()
    {
        var sends = 0;
        using var host = ResilienceTestHost.Create("mutable-options", new DelegateHttpMessageHandler((_, _) =>
        {
            sends++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }), options =>
        {
            options.Mode = BingHttpResilienceMode.Hedging;
            options.CanHedgeRequest = _ => true;
            options.Hedging.Endpoints.Add(new("primary", new Uri("https://service.test")));
            options.Hedging.ValidateCustomTransport = handler => handler is DelegateHttpMessageHandler;
        });
        using var request = ResilienceTestResponses.Get();
        request.Options.Set(new HttpRequestOptionsKey<MutableContainer>("mutable"), new(new List<int> { 1 }));
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));
        Assert.Equal(0, sends);
    }

    [Theory]
    [InlineData("https", "http", false)]
    [InlineData("https", "https", true)]
    [InlineData("http", "http", true)]
    [InlineData("http", "https", true)]
    public void HedgingRejectsTlsDowngradeAtRegistration(string primary, string backup, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("fences").UseDefaultProvider("fences");
        var sends = 0;
        var builder = services.AddHttpClient("tls-boundary").ConfigurePrimaryHttpMessageHandler(() =>
            new DelegateHttpMessageHandler((_, _) =>
            {
                sends++;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }));
        void Register() => builder.AddBingHttpResilience("tls-boundary", o =>
        {
            o.Mode = BingHttpResilienceMode.Hedging;
            o.CanHedgeRequest = _ => true;
            o.Hedging.Endpoints.Add(new("primary", new Uri(primary + "://primary.test")));
            o.Hedging.Endpoints.Add(new("backup", new Uri(backup + "://backup.test")));
            o.Hedging.ForwardHeaders.Add("X-Api-Key");
            o.Hedging.ValidateCustomTransport = h => h is DelegateHttpMessageHandler;
        });
        if (allowed) Register();
        else Assert.Throws<ArgumentException>(Register);
        Assert.Equal(0, sends);
    }

    [Fact]
    public void HttpOptionsCopyIncludesProviderRoutingAndIndependentCollections()
    {
        var source = new BingHttpResilienceOptions { ProviderName = "other", Mode = BingHttpResilienceMode.Hedging };
        source.Hedging.Endpoints.Add(new("one", new Uri("https://one.test")));
        source.Hedging.ForwardHeaders.Add("X-Trace");
        source.RateLimit.Enabled = true; source.RateLimit.TokenLimit = 4;
        source.RateLimit.TokensPerPeriod = 1; source.RateLimit.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        source.CircuitBreaker.EnableManualControl = true;
        var copy = new BingHttpResilienceOptions(); source.CopyTo(copy);
        source.Hedging.Endpoints.Clear(); source.Hedging.ForwardHeaders.Clear();
        Assert.Equal("other", copy.ProviderName);
        Assert.Equal(BingHttpResilienceMode.Hedging, copy.Mode);
        Assert.Single(copy.Hedging.Endpoints);
        Assert.Contains("X-Trace", copy.Hedging.ForwardHeaders);
        Assert.Equal(4, copy.RateLimit.TokenLimit);
        Assert.True(copy.CircuitBreaker.EnableManualControl);
    }
}

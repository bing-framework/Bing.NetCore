using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Bing.Http.Resilience;

/// <summary>为原 builder 添加中性 HTTP 恢复处理器；后续 handler 位于每次尝试内侧。</summary>
public static class BingHttpResilienceExtensions
{
    public static IHttpClientBuilder AddBingHttpResilience(this IHttpClientBuilder builder,
        string profileName, Action<BingHttpResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(profileName)) throw new ArgumentException("恢复配置名称不能为空。", nameof(profileName));
        if (builder.Services.Any(d => d.ImplementationInstance is Registration r && r.ClientName == builder.Name))
            throw new InvalidOperationException($"HttpClient '{builder.Name}' 已注册 Bing 恢复管线。");
        var configured = new BingHttpResilienceOptions(); configure?.Invoke(configured);
        var options = configured.Snapshot();
        var registration = new Registration(builder.Name, profileName, options);
        builder.Services.AddSingleton(registration);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, SinglePipelineFilter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HttpStartupValidator>());
        RegisterPipelines(builder.Services, registration);
        builder.AddHttpMessageHandler(sp => new BingHttpResilienceHandler(registration,
            sp.GetRequiredService<IResiliencePipelineProvider>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetServices<IBingHttpRoutingStrategy>().SingleOrDefault(x => x.ClientName == builder.Name),
            sp.GetServices<IBingHttpReplayStrategy>().SingleOrDefault(x => x.ClientName == builder.Name)));
        return builder;
    }

    private static void RegisterPipelines(IServiceCollection services, Registration r)
    {
        var o = r.Options;
        void Add(string purpose, string partition, Action<ResiliencePipelineOptions<HttpResponseMessage>> configure)
        {
            services.AddBingResiliencePipeline<HttpResponseMessage>(r.ClientName, p =>
            {
                p.Purpose = purpose; p.Partition = partition; p.ProviderName = o.ProviderName;
                p.DiscardResultAsync = response => { response.Dispose(); return default; };
                configure(p);
            });
        }
        Add("http.send", "", p =>
        {
            p.ExecutionTimeout = o.RequestTimeout;
            p.Concurrency.Enabled = o.Concurrency.Enabled;
            p.Concurrency.PermitLimit = o.Concurrency.PermitLimit; p.Concurrency.QueueLimit = o.Concurrency.QueueLimit;
        });
        Add("http.retry", "", p =>
        {
            p.Retry.MaxRetryAttempts = o.Retry.MaxRetryAttempts; p.Retry.Delay = o.Retry.Delay;
            p.Retry.MaxDelay = o.Retry.MaxDelay; p.Retry.UseJitter = o.Retry.UseJitter;
            p.Retry.ShouldHandle = a => BingHttpRequestState.From(a.Context).CanRetry && HttpFailureClassifier.IsTransient(a.Outcome, true);
            p.Retry.DelayGenerator = a => a.Outcome.HasResult ? BingHttpRequestState.From(a.Context).RetryAfter(a.Outcome.Result!) : null;
        });
        Add("http.rate", "", p =>
        {
            p.RateLimit.Enabled = o.RateLimit.Enabled; p.RateLimit.TokenLimit = o.RateLimit.TokenLimit;
            p.RateLimit.TokensPerPeriod = o.RateLimit.TokensPerPeriod;
            p.RateLimit.ReplenishmentPeriod = o.RateLimit.ReplenishmentPeriod; p.RateLimit.QueueLimit = o.RateLimit.QueueLimit;
        });
        void Attempt(string purpose, string partition, bool endpoint)
        {
            Add(purpose, partition, p =>
            {
                p.AttemptTimeout = o.AttemptTimeout;
                foreach (var observer in o.AttemptObservers) p.AttemptObservers.Add(observer);
                p.CircuitBreaker.Enabled = o.CircuitBreaker.Enabled;
                p.CircuitBreaker.EnableManualControl = o.CircuitBreaker.EnableManualControl;
                p.CircuitBreaker.FailureRatio = o.CircuitBreaker.FailureRatio;
                p.CircuitBreaker.MinimumThroughput = o.CircuitBreaker.MinimumThroughput;
                p.CircuitBreaker.SamplingDuration = o.CircuitBreaker.SamplingDuration;
                p.CircuitBreaker.BreakDuration = o.CircuitBreaker.BreakDuration;
                p.CircuitBreaker.ShouldHandle = a => HttpFailureClassifier.IsTransient(a.Outcome, false);
                p.Concurrency.Enabled = endpoint;
                p.Concurrency.PermitLimit = o.Hedging.EndpointPermitLimit;
                p.Concurrency.QueueLimit = o.Hedging.EndpointQueueLimit;
            });
        }
        Attempt("http.attempt", "", false);
        if (o.UseDynamicRouting) Attempt("http.endpoint", "", true);
        if (o.Mode == BingHttpResilienceMode.Hedging)
        {
            Add("http.hedge", "", p =>
            {
                p.Hedging.Enabled = true; p.Hedging.AllowParallelExecution = true;
                p.Hedging.Delay = o.Hedging.Delay; p.Hedging.MaxHedgedAttempts = o.Hedging.MaxHedgedAttempts;
                p.Hedging.ShouldHandle = a => HttpFailureClassifier.IsTransient(a.Outcome, true) ||
                    a.Outcome.Exception is ResilienceCircuitOpenException { Identity.Purpose: "http.endpoint" } ||
                    a.Outcome.Exception is ResilienceRejectedException { Reason: ResilienceRejectionReason.Concurrency, Identity.Purpose: "http.endpoint" };
            });
            foreach (var endpoint in o.Hedging.Endpoints) Attempt("http.endpoint", endpoint.Id, true);
        }
    }

    internal sealed record Registration(string ClientName, string ProfileName, BingHttpResilienceOptions Options);

    internal sealed class SinglePipelineFilter(IEnumerable<Registration> registrations, IEnumerable<IBingHttpRoutingStrategy> routes, IEnumerable<IBingHttpReplayStrategy> replays) : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            var registration = registrations.SingleOrDefault(r => r.ClientName == builder.Name);
            if (registration is null) return;
            if (builder.AdditionalHandlers.Count(h => h is BingHttpResilienceHandler) != 1 ||
                builder.AdditionalHandlers.Any(h => h.GetType().FullName is
                    "Microsoft.Extensions.Http.Resilience.ResilienceHandler" or "Microsoft.Extensions.Http.PolicyHttpMessageHandler"))
                throw new InvalidOperationException($"HttpClient '{builder.Name}' 必须且只能包含一条已知恢复管线。");
            if (registration.Options.UseDynamicRouting && routes.Count(x => x.ClientName == registration.ClientName) != 1)
                throw new ResilienceConfigurationException("动态发现需要唯一的路由策略。");
            if (replays.Count(x => x.ClientName == registration.ClientName) > 1) throw new ResilienceConfigurationException("写重放策略必须唯一。");
            if (registration.Options.Mode != BingHttpResilienceMode.Hedging && !registration.Options.UseDynamicRouting &&
                !replays.Any(x => x.ClientName == registration.ClientName)) return;
            var valid = builder.PrimaryHandler switch
            {
                SocketsHttpHandler sockets => !sockets.AllowAutoRedirect && !sockets.UseCookies,
                HttpClientHandler client => !client.AllowAutoRedirect && !client.UseCookies,
                _ => registration.Options.Hedging.ValidateCustomTransport?.Invoke(builder.PrimaryHandler) == true
            };
            if (!valid) throw new ResilienceConfigurationException($"HttpClient '{builder.Name}' 的 Hedging transport 必须关闭重定向、共享 Cookie，并支持并发。");
        };
    }
    internal sealed class HttpStartupValidator(IEnumerable<Registration> registrations, IHttpClientFactory factory, IResiliencePipelineProvider pipelines) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            pipelines.Validate();
            foreach (var registration in registrations)
            {
                using var client = factory.CreateClient(registration.ClientName);
            }
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

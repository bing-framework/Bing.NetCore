using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpRegistrationTests
{
    [Fact]
    public void Reject_null_http_client_builder()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BingHttpResilienceExtensions.AddBingHttpResilience(null!, "profile"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Reject_blank_profile_name(string profileName)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient("blank-profile");

        Assert.Throws<ArgumentException>(() => builder.AddBingHttpResilience(profileName));
    }

    [Theory]
    [InlineData("request-timeout")]
    [InlineData("attempt-exceeds-total")]
    [InlineData("retry-count")]
    [InlineData("retry-delay-range")]
    [InlineData("circuit-breaker")]
    [InlineData("concurrency")]
    public void Reject_invalid_resilience_options(string invalidOption)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient("invalid-options-" + invalidOption);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddBingHttpResilience("profile", options =>
        {
            switch (invalidOption)
            {
                case "request-timeout":
                    options.RequestTimeout = TimeSpan.FromMilliseconds(1);
                    break;
                case "attempt-exceeds-total":
                    options.AttemptTimeout = options.RequestTimeout + TimeSpan.FromMilliseconds(1);
                    break;
                case "retry-count":
                    options.Retry.MaxRetryAttempts = 101;
                    break;
                case "retry-delay-range":
                    options.Retry.Delay = TimeSpan.FromSeconds(2);
                    options.Retry.MaxDelay = TimeSpan.FromSeconds(1);
                    break;
                case "circuit-breaker":
                    options.CircuitBreaker.FailureRatio = 0;
                    break;
                case "concurrency":
                    options.Concurrency.PermitLimit = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidOption));
            }
        }));
    }

    [Fact]
    public void Reject_duplicate_bing_pipeline_registration_for_one_client()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient("duplicate-pipeline");
        builder.AddBingHttpResilience("first-profile");

        Assert.Throws<InvalidOperationException>(() => builder.AddBingHttpResilience("second-profile"));
    }

    [Fact]
    public void Reject_a_second_resilience_handler_in_the_built_http_client_chain()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient("stacked-pipeline");
        builder.AddResilienceHandler("preexisting", _ => { });
        builder.AddBingHttpResilience("bing-profile");
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient("stacked-pipeline"));
    }
}

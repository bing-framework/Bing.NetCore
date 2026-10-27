using System.Security.Authentication;
using Bing.Resilience;

namespace Bing.Http.Resilience;

internal static class HttpFailureClassifier
{
    internal static bool IsTransient(ResilienceOutcome<HttpResponseMessage> outcome, bool includeRateLimit)
    {
        if (outcome.Exception is IResilienceScopedRejection scoped) return includeRateLimit && scoped.CanMoveToNextEndpoint;
        if (outcome.Exception is ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Attempt }) return true;
        if (outcome.Exception is HttpRequestException exception)
        {
            for (Exception? inner = exception; inner is not null; inner = inner.InnerException)
                if (inner is AuthenticationException) return false;
#if NET8_0_OR_GREATER
            if (exception.HttpRequestError is HttpRequestError.SecureConnectionError or HttpRequestError.HttpProtocolError or
                HttpRequestError.VersionNegotiationError or HttpRequestError.UserAuthenticationError or HttpRequestError.ConfigurationLimitExceeded)
                return false;
#endif
            return exception.StatusCode is null || Status((int)exception.StatusCode, includeRateLimit);
        }
        return outcome.HasResult && outcome.Result is not null && Status((int)outcome.Result.StatusCode, includeRateLimit);
    }
    private static bool Status(int code, bool rate) => code is 408 or 500 or 502 or 503 or 504 || (rate && code == 429);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

using Polly;

using Refit;

namespace GitReleaseNoteGenerator.Infrastructure;

/// <summary>
/// Provides a Polly-based retry pipeline for GitHub API calls,
/// handling rate limits, server errors, and transient failures.
/// </summary>
public static partial class RetryHandler
{
    /// <summary>The maximum number of retry attempts before giving up.</summary>
    private const int MaxRetries = 3;

    /// <summary>The GitHub header carrying the number of requests remaining in the current rate-limit window.</summary>
    private const string RateLimitRemainingHeader = "x-ratelimit-remaining";

    /// <summary>The GitHub header carrying the UTC epoch second at which the rate-limit window resets.</summary>
    private const string RateLimitResetHeader = "x-ratelimit-reset";

    /// <summary>Creates a resilience pipeline for GitHub API calls with exponential backoff.</summary>
    /// <param name="logger">Logger for retry event information.</param>
    /// <returns>A configured resilience pipeline.</returns>
    public static ResiliencePipeline CreatePipeline(ILogger logger) =>
        CreatePipeline(logger, TimeProvider.System);

    /// <summary>Creates a resilience pipeline for GitHub API calls with exponential backoff.</summary>
    /// <param name="logger">Logger for retry event information.</param>
    /// <param name="timeProvider">The time provider used to calculate rate limit reset delays.</param>
    /// <returns>A configured resilience pipeline.</returns>
    public static ResiliencePipeline CreatePipeline(ILogger logger, TimeProvider timeProvider) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<ApiException>(ShouldRetry)
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                DelayGenerator = args => ValueTask.FromResult(CalculateRateLimitDelay(args.Outcome.Exception, timeProvider)),
                OnRetry = args =>
                {
                    LogRetry(logger, args.Outcome.Exception, args.AttemptNumber + 1, MaxRetries, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

    /// <summary>
    /// Calculates the retry delay for a GitHub rate limit response. A primary rate limit carries a
    /// reset timestamp (<c>x-ratelimit-reset</c>); an abuse/secondary rate limit carries a
    /// <c>Retry-After</c> hint. Both are honored so the tool waits exactly as long as GitHub asks.
    /// Any other exception (or a rate limit with no usable hint) returns null, letting the pipeline
    /// apply its exponential backoff instead.
    /// </summary>
    /// <param name="exception">The exception that triggered the retry, if any.</param>
    /// <param name="timeProvider">The time provider used to get the current time.</param>
    /// <returns>The rate limit reset delay, or null to use the default retry delay.</returns>
    internal static TimeSpan? CalculateRateLimitDelay(Exception? exception, TimeProvider timeProvider)
    {
        if (exception is not ApiException apiException)
        {
            return null;
        }

        if (IsPrimaryRateLimit(apiException)
            && TryGetHeaderValue(apiException.Headers, RateLimitResetHeader, out var resetValue)
            && long.TryParse(resetValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetEpoch))
        {
            var delay = DateTimeOffset.FromUnixTimeSeconds(resetEpoch) - timeProvider.GetUtcNow();
            return delay > TimeSpan.Zero ? delay + TimeSpan.FromSeconds(1) : null;
        }

        var retryAfter = GetRetryAfter(apiException);
        return retryAfter > TimeSpan.Zero ? retryAfter + TimeSpan.FromSeconds(1) : null;
    }

    /// <summary>
    /// Determines whether an API failure is transient or rate-limited and therefore worth retrying.
    /// Server errors, 429 responses, primary rate limits, and abuse/secondary limits (with a
    /// <c>Retry-After</c> hint) are retried; other 4xx responses (authorization, not found) are not.
    /// </summary>
    /// <param name="exception">The API exception to inspect.</param>
    /// <returns>True when the failure should be retried; otherwise, false.</returns>
    private static bool ShouldRetry(ApiException exception) =>
        (int)exception.StatusCode >= (int)HttpStatusCode.InternalServerError
        || exception.StatusCode == HttpStatusCode.TooManyRequests
        || IsPrimaryRateLimit(exception)
        || GetRetryAfter(exception) > TimeSpan.Zero;

    /// <summary>
    /// Determines whether an API failure is a primary rate limit: a 403/429 response whose
    /// <c>x-ratelimit-remaining</c> header has reached zero.
    /// </summary>
    /// <param name="exception">The API exception to inspect.</param>
    /// <returns>True when the failure is a primary rate limit; otherwise, false.</returns>
    private static bool IsPrimaryRateLimit(ApiException exception) =>
        exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
        && TryGetHeaderValue(exception.Headers, RateLimitRemainingHeader, out var remaining)
        && remaining == "0";

    /// <summary>Gets the <c>Retry-After</c> delay hint from an API failure, if present.</summary>
    /// <param name="exception">The API exception to inspect.</param>
    /// <returns>The requested wait, or <see cref="TimeSpan.Zero"/> when none was supplied.</returns>
    private static TimeSpan GetRetryAfter(ApiException exception) =>
        exception.Headers?.RetryAfter?.Delta ?? TimeSpan.Zero;

    /// <summary>Reads the first value of a response header, if the header is present.</summary>
    /// <param name="headers">The response headers to read from, or null.</param>
    /// <param name="name">The header name.</param>
    /// <param name="value">The first header value, or null when the header is absent.</param>
    /// <returns>True when the header was present; otherwise, false.</returns>
    private static bool TryGetHeaderValue(HttpResponseHeaders? headers, string name, out string? value)
    {
        if (headers is not null
            && headers.TryGetValues(name, out var values)
            && values is IReadOnlyList<string> { Count: > 0 } list)
        {
            value = list[0];
            return value is not null;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Logs a retry attempt with the current attempt number, maximum retries, and delay.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception that triggered the retry, if any.</param>
    /// <param name="attempt">The current attempt number (1-based).</param>
    /// <param name="max">The maximum number of retries.</param>
    /// <param name="delay">The delay before the next retry.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub API call failed (attempt {Attempt}/{Max}), retrying in {Delay}...")]
    private static partial void LogRetry(ILogger logger, Exception? exception, int attempt, int max, TimeSpan delay);
}

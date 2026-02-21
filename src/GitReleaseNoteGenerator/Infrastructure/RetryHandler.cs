// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

using Octokit;

using Polly;
using Polly.Retry;

namespace GitReleaseNoteGenerator.Infrastructure;

/// <summary>
/// Provides a Polly-based retry pipeline for GitHub API calls,
/// handling rate limits, server errors, and transient failures.
/// </summary>
public static partial class RetryHandler
{
    /// <summary>
    /// The maximum number of retry attempts before giving up.
    /// </summary>
    private const int MaxRetries = 3;

    /// <summary>
    /// Creates a resilience pipeline for GitHub API calls with exponential backoff.
    /// </summary>
    /// <param name="logger">Logger for retry event information.</param>
    /// <returns>A configured resilience pipeline.</returns>
    public static ResiliencePipeline CreatePipeline(ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<RateLimitExceededException>()
                    .Handle<ApiException>(ex => ex.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                DelayGenerator = static args =>
                {
                    if (args.Outcome.Exception is RateLimitExceededException rateLimitEx)
                    {
                        var resetTime = rateLimitEx.Reset;
                        var delay = resetTime - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                        {
                            return ValueTask.FromResult<TimeSpan?>(delay + TimeSpan.FromSeconds(1));
                        }
                    }

                    return ValueTask.FromResult<TimeSpan?>(null);
                },
                OnRetry = args =>
                {
                    LogRetry(logger, args.Outcome.Exception, args.AttemptNumber + 1, MaxRetries, args.RetryDelay);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();

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

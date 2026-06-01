// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;

using Octokit;

using Polly;

namespace GitReleaseNoteGenerator.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="RetryHandler"/>.
/// </summary>
public class RetryHandlerTests
{
    /// <summary>
    /// Tests that the pipeline runs a successful operation and returns its result.
    /// </summary>
    [Test]
    public async Task CreatePipeline_WithSuccessfulOperation_ReturnsResult()
    {
        var pipeline = RetryHandler.CreatePipeline(NullLogger.Instance);

        var result = await pipeline.ExecuteAsync(async _ =>
        {
            await Task.Yield();
            return "ok";
        });

        await Assert.That(result).IsEqualTo("ok");
    }

    /// <summary>
    /// Tests that the pipeline retries a transient failure and then succeeds, exercising the
    /// retry/backoff path.
    /// </summary>
    [Test]
    public async Task CreatePipeline_WithTransientFailure_RetriesThenSucceeds()
    {
        const int succeedOnAttempt = 2;
        var pipeline = RetryHandler.CreatePipeline(NullLogger.Instance);
        var attempts = 0;

        var result = await pipeline.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < succeedOnAttempt)
            {
                throw new HttpRequestException("transient");
            }

            await Task.Yield();
            return attempts;
        });

        await Assert.That(result).IsEqualTo(succeedOnAttempt);
    }

    /// <summary>
    /// Tests that a rate-limit exception whose reset is in the future yields a positive delay.
    /// </summary>
    [Test]
    public async Task CalculateRateLimitDelay_WithFutureReset_ReturnsPositiveDelay()
    {
        var now = DateTimeOffset.UnixEpoch;
        var exception = CreateRateLimitException(now.AddMinutes(5).ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNotNull();
        await Assert.That(delay!.Value > TimeSpan.Zero).IsTrue();
    }

    /// <summary>
    /// Tests that a rate-limit exception whose reset is in the past yields no delay.
    /// </summary>
    [Test]
    public async Task CalculateRateLimitDelay_WithPastReset_ReturnsNull()
    {
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var exception = CreateRateLimitException(DateTimeOffset.UnixEpoch.ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNull();
    }

    /// <summary>
    /// Tests that a non-rate-limit exception yields no rate-limit delay.
    /// </summary>
    [Test]
    public async Task CalculateRateLimitDelay_WithNonRateLimitException_ReturnsNull()
    {
        var delay = RetryHandler.CalculateRateLimitDelay(new HttpRequestException("boom"), TimeProvider.System);

        await Assert.That(delay).IsNull();
    }

    /// <summary>
    /// Creates a <see cref="RateLimitExceededException"/> whose rate-limit reset is the given epoch.
    /// </summary>
    /// <param name="resetEpochSeconds">The reset time as UTC epoch seconds.</param>
    /// <returns>The constructed exception.</returns>
    private static RateLimitExceededException CreateRateLimitException(long resetEpochSeconds)
    {
        var rateLimit = new RateLimit(5000, 0, resetEpochSeconds);
        var apiInfo = new ApiInfo(new Dictionary<string, Uri>(), [], [], "etag", rateLimit, TimeSpan.Zero);
        return new RateLimitExceededException(new FakeResponse(apiInfo));
    }

    /// <summary>
    /// A fixed-time <see cref="TimeProvider"/> for deterministic delay calculations.
    /// </summary>
    /// <param name="now">The instant to report from <see cref="GetUtcNow"/>.</param>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// A minimal <see cref="IResponse"/> exposing a supplied <see cref="ApiInfo"/>.
    /// </summary>
    /// <param name="apiInfo">The API info to expose.</param>
    private sealed class FakeResponse(ApiInfo apiInfo) : IResponse
    {
        /// <inheritdoc/>
        public object Body => "{}";

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <inheritdoc/>
        public ApiInfo ApiInfo { get; } = apiInfo;

        /// <inheritdoc/>
        public HttpStatusCode StatusCode => HttpStatusCode.Forbidden;

        /// <inheritdoc/>
        public string ContentType => "application/json";
    }
}

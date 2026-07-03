// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;

using Polly;

using Refit;

namespace GitReleaseNoteGenerator.Tests.Infrastructure;

/// <summary>Tests for <see cref="RetryHandler"/>.</summary>
public class RetryHandlerTests
{
    /// <summary>Tests that the pipeline runs a successful operation and returns its result.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>Tests that the pipeline retries a transient failure and then succeeds, exercising the retry/backoff path.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>Tests that a primary rate-limit response whose reset is in the future yields a positive delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelay_WithFutureReset_ReturnsPositiveDelay()
    {
        var now = DateTimeOffset.UnixEpoch;
        var exception = await CreateRateLimitExceptionAsync(now.AddMinutes(5).ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNotNull();
        await Assert.That(delay!.Value > TimeSpan.Zero).IsTrue();
    }

    /// <summary>Tests that a primary rate-limit response whose reset is in the past yields no delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelay_WithPastReset_ReturnsNull()
    {
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var exception = await CreateRateLimitExceptionAsync(DateTimeOffset.UnixEpoch.ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNull();
    }

    /// <summary>Tests that a non-rate-limit exception yields no rate-limit delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelay_WithNonRateLimitException_ReturnsNull()
    {
        var delay = RetryHandler.CalculateRateLimitDelay(new HttpRequestException("boom"), TimeProvider.System);

        await Assert.That(delay).IsNull();
    }

    /// <summary>
    /// Tests that an abuse/secondary rate-limit response carrying a "Retry-After" hint yields a
    /// delay at least as long as the requested wait.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelay_WithRetryAfter_HonorsRetryAfter()
    {
        const int retryAfterSeconds = 30;
        var exception = await CreateRetryAfterExceptionAsync(retryAfterSeconds);

        var delay = RetryHandler.CalculateRateLimitDelay(exception, TimeProvider.System);

        await Assert.That(delay).IsNotNull();
        await Assert.That(delay!.Value >= TimeSpan.FromSeconds(retryAfterSeconds)).IsTrue();
    }

    /// <summary>
    /// Tests that a rate-limit response with no reset or retry hint yields no explicit delay, so the
    /// pipeline falls back to its exponential backoff.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelay_WithNoHint_ReturnsNull()
    {
        var exception = await CreateApiExceptionAsync(HttpStatusCode.Forbidden, static _ => { });

        var delay = RetryHandler.CalculateRateLimitDelay(exception, TimeProvider.System);

        await Assert.That(delay).IsNull();
    }

    /// <summary>Creates a Refit <see cref="ApiException"/> for a primary rate limit whose window resets at the given epoch.</summary>
    /// <param name="resetEpochSeconds">The reset time as UTC epoch seconds.</param>
    /// <returns>The constructed exception.</returns>
    private static Task<ApiException> CreateRateLimitExceptionAsync(long resetEpochSeconds) =>
        CreateApiExceptionAsync(HttpStatusCode.Forbidden, headers =>
        {
            headers.Add("x-ratelimit-remaining", "0");
            headers.Add("x-ratelimit-reset", resetEpochSeconds.ToString(CultureInfo.InvariantCulture));
        });

    /// <summary>Creates a Refit <see cref="ApiException"/> carrying a "Retry-After" header hint.</summary>
    /// <param name="retryAfterSeconds">The requested wait, in seconds.</param>
    /// <returns>The constructed exception.</returns>
    private static Task<ApiException> CreateRetryAfterExceptionAsync(int retryAfterSeconds) =>
        CreateApiExceptionAsync(HttpStatusCode.Forbidden, headers =>
            headers.Add("Retry-After", retryAfterSeconds.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Creates a Refit <see cref="ApiException"/> for the given status with configured headers.</summary>
    /// <param name="status">The response status code.</param>
    /// <param name="configureHeaders">Applies the response headers.</param>
    /// <returns>The constructed exception.</returns>
    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode status, Action<HttpResponseHeaders> configureHeaders)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/");
        using var response = new HttpResponseMessage(status) { RequestMessage = request };
        configureHeaders(response.Headers);
        return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings()).ConfigureAwait(false);
    }

    /// <summary>A fixed-time <see cref="TimeProvider"/> for deterministic delay calculations.</summary>
    /// <param name="now">The instant to report from <see cref="GetUtcNow"/>.</param>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => now;
    }
}

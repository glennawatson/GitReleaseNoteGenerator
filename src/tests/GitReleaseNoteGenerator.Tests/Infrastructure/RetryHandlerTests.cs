// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;

using Refit;

namespace GitReleaseNoteGenerator.Tests.Infrastructure;

/// <summary>Tests for <see cref="RetryHandler"/>.</summary>
public class RetryHandlerTests
{
    /// <summary>The number of attempts made when every one fails: the first plus three retries.</summary>
    private const int AttemptsWhenAllFail = 4;

    /// <summary>Tests that a successful operation runs once and returns its result.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExecuteAsyncWithSuccessfulOperationReturnsResult()
    {
        var retry = new RetryHandler(NullLogger.Instance, new ImmediateTimeProvider());
        var attempts = new StrongBox<int>(0);

        var result = await retry.ExecuteAsync(
            static async (state, _) =>
            {
                state.Value++;
                await Task.Yield();
                return "ok";
            },
            attempts,
            CancellationToken.None);

        await Assert.That(result).IsEqualTo("ok");
        await Assert.That(attempts.Value).IsEqualTo(1);
    }

    /// <summary>Tests that a transient failure is retried and the later success is returned.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExecuteAsyncWithTransientFailureRetriesThenSucceeds()
    {
        const int succeedOnAttempt = 2;
        var retry = new RetryHandler(NullLogger.Instance, new ImmediateTimeProvider());
        var attempts = new StrongBox<int>(0);

        var result = await retry.ExecuteAsync(
            static async (state, _) =>
            {
                state.Value++;
                if (state.Value < succeedOnAttempt)
                {
                    throw new HttpRequestException("transient");
                }

                await Task.Yield();
                return state.Value;
            },
            attempts,
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(succeedOnAttempt);
    }

    /// <summary>Tests that a failure that never clears is retried three times and then rethrown.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExecuteAsyncWithPersistentFailureRethrowsAfterMaxRetries()
    {
        var retry = new RetryHandler(NullLogger.Instance, new ImmediateTimeProvider());
        var attempts = new StrongBox<int>(0);

        await Assert.That(async () => await retry.ExecuteAsync<StrongBox<int>, int>(
            static (state, _) =>
            {
                state.Value++;
                throw new HttpRequestException("down");
            },
            attempts,
            CancellationToken.None)).Throws<HttpRequestException>();

        await Assert.That(attempts.Value).IsEqualTo(AttemptsWhenAllFail);
    }

    /// <summary>Tests that a non-transient API failure, such as not found, is thrown at once without a retry.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExecuteAsyncWithNotFoundDoesNotRetry()
    {
        var retry = new RetryHandler(NullLogger.Instance, new ImmediateTimeProvider());
        var notFound = await CreateApiExceptionAsync(HttpStatusCode.NotFound, static _ => { });
        var state = (Attempts: new StrongBox<int>(0), Failure: notFound);

        await Assert.That(async () => await retry.ExecuteAsync<(StrongBox<int> Attempts, ApiException Failure), int>(
            static (state, _) =>
            {
                state.Attempts.Value++;
                throw state.Failure;
            },
            state,
            CancellationToken.None)).Throws<ApiException>();

        await Assert.That(state.Attempts.Value).IsEqualTo(1);
    }

    /// <summary>Tests that the backoff doubles per attempt and stays within the jitter band around it.</summary>
    /// <param name="attempt">The zero-based attempt that failed.</param>
    /// <param name="expectedSeconds">The un-jittered delay for that attempt, in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments(0, 2D)]
    [Arguments(1, 4D)]
    [Arguments(2, 8D)]
    public async Task CalculateBackoffDelayDoublesWithinJitterBand(int attempt, double expectedSeconds)
    {
        const double jitter = 0.25;

        var delay = RetryHandler.CalculateBackoffDelay(attempt);

        await Assert.That(delay.TotalSeconds).IsGreaterThanOrEqualTo(expectedSeconds * (1 - jitter));
        await Assert.That(delay.TotalSeconds).IsLessThanOrEqualTo(expectedSeconds * (1 + jitter));
    }

    /// <summary>Tests that a primary rate-limit response whose reset is in the future yields a positive delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelayWithFutureResetReturnsPositiveDelay()
    {
        const int resetInMinutes = 5;
        var now = DateTimeOffset.UnixEpoch;
        var exception = await CreateRateLimitExceptionAsync(now.AddMinutes(resetInMinutes).ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNotNull();
        await Assert.That(delay!.Value > TimeSpan.Zero).IsTrue();
    }

    /// <summary>Tests that a primary rate-limit response whose reset is in the past yields no delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelayWithPastResetReturnsNull()
    {
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var exception = await CreateRateLimitExceptionAsync(DateTimeOffset.UnixEpoch.ToUnixTimeSeconds());

        var delay = RetryHandler.CalculateRateLimitDelay(exception, new FixedTimeProvider(now));

        await Assert.That(delay).IsNull();
    }

    /// <summary>Tests that a non-rate-limit exception yields no rate-limit delay.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CalculateRateLimitDelayWithNonRateLimitExceptionReturnsNull()
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
    public async Task CalculateRateLimitDelayWithRetryAfterHonorsRetryAfter()
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
    public async Task CalculateRateLimitDelayWithNoHintReturnsNull()
    {
        var exception = await CreateApiExceptionAsync(HttpStatusCode.Forbidden, static _ => { });

        var delay = RetryHandler.CalculateRateLimitDelay(exception, TimeProvider.System);

        await Assert.That(delay).IsNull();
    }

    /// <summary>Creates a Refit <see cref="ApiException"/> for a primary rate limit whose window resets at the given epoch.</summary>
    /// <param name="resetEpochSeconds">The reset time as UTC epoch seconds.</param>
    /// <returns>The constructed exception.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ApiException> CreateRateLimitExceptionAsync(long resetEpochSeconds) =>
        CreateApiExceptionAsync(HttpStatusCode.Forbidden, headers =>
        {
            headers.Add("x-ratelimit-remaining", "0");
            headers.Add("x-ratelimit-reset", resetEpochSeconds.ToString(CultureInfo.InvariantCulture));
        });

    /// <summary>Creates a Refit <see cref="ApiException"/> carrying a "Retry-After" header hint.</summary>
    /// <param name="retryAfterSeconds">The requested wait, in seconds.</param>
    /// <returns>The constructed exception.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        return await ApiException.Create(request, HttpMethod.Get, response, new()).ConfigureAwait(false);
    }

    /// <summary>A fixed-time <see cref="TimeProvider"/> for deterministic delay calculations.</summary>
    /// <param name="now">The instant to report from <see cref="GetUtcNow"/>.</param>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>A <see cref="TimeProvider"/> whose timers fire straight away, so the waits between retries take no time.</summary>
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        /// <inheritdoc/>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // Fired on the pool rather than inline, so the timer exists before the delay it completes observes it.
            _ = ThreadPool.UnsafeQueueUserWorkItem(static args => args.Callback(args.State), (Callback: callback, State: state), preferLocal: false);
            return new InertTimer();
        }

        /// <summary>A timer that has already fired, so changing or disposing it does nothing.</summary>
        private sealed class InertTimer : ITimer
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

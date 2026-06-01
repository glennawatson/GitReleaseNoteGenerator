// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;

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
}

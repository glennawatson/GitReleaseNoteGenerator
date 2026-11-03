// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:property TargetFramework=net11.0

using System.Diagnostics;
using static System.Environment;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var cancellationToken = timeout.Token;
var packages = Directory.GetFiles(Path.Combine(GetEnvironmentVariable("GITHUB_WORKSPACE")!, "signed"), "*.nupkg");
if (packages is [])
{
    Console.WriteLine("::error::No signed packages were downloaded.");
    return 1;
}

foreach (var package in packages.Order(StringComparer.Ordinal))
{
    cancellationToken.ThrowIfCancellationRequested();
    var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    foreach (var argument in new[] { "nuget", "push", package, "--source", "https://api.nuget.org/v3/index.json", "--api-key", GetEnvironmentVariable("NUGET_API_KEY")! })
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var push = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the NuGet push.");
    await push.WaitForExitAsync(cancellationToken);
    if (push.ExitCode != 0)
    {
        return push.ExitCode;
    }
}

return 0;

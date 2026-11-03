// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:property TargetFramework=net11.0

if (args is not [var choice, var preRelease, var versionOverride])
{
    Console.WriteLine("::error::Expected the bump, pre-release and version override arguments.");
    return 2;
}

var bump = choice is "(pick one)" ? string.Empty : choice;
string? error = (bump, preRelease, versionOverride) switch
{
    ("", _, "") => "Pick a bump level (major, minor or patch), or give a version override.",
    (not "", _, not "") => "'bump' and 'versionOverride' are mutually exclusive: set exactly one.",
    (_, not "", not "") => "'preRelease' cannot be combined with 'versionOverride': put any pre-release suffix in the override itself.",
    (_, _, ['v' or 'V', ..]) => "Give 'versionOverride' without the 'v' tag prefix; the tag adds it.",
    _ => null,
};

if (error is not null)
{
    Console.WriteLine($"::error::{error}");
    return 1;
}

return 0;

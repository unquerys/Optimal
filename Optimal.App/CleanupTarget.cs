using System;

namespace Optimal.App;

internal sealed record CleanupTarget(string Name, string Path, TimeSpan MinimumAge);

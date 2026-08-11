using Microsoft.Extensions.Logging;

namespace Optimal.Core.Operations;

public sealed class OperationContext
{
	public required ILogger Logger { get; init; }

	public required IProcessRunner Process { get; init; }

	public bool DryRun { get; init; }
}

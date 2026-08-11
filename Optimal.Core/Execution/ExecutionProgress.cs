using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Optimal.Core.Safety;

namespace Optimal.Core.Execution;

public sealed record ExecutionProgress
{
	public required ExecutionPhase Phase { get; init; }

	public string? TweakId { get; init; }

	public string? TweakName { get; init; }

	public StepOutcome? Outcome { get; init; }

	public int Completed { get; init; }

	public int Total { get; init; }

	public string? Message { get; init; }

	public double Fraction
	{
		get
		{
			if (Total != 0)
			{
				return (double)Completed / (double)Total;
			}
			return 0.0;
		}
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ExecutionProgress(ExecutionProgress original)
	{
		Phase = original.Phase;
		TweakId = original.TweakId;
		TweakName = original.TweakName;
		Outcome = original.Outcome;
		Completed = original.Completed;
		Total = original.Total;
		Message = original.Message;
	}

	public ExecutionProgress()
	{
	}
}

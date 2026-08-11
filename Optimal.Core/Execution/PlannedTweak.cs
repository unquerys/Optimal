using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Optimal.Core.Manifest;

namespace Optimal.Core.Execution;

public sealed record PlannedTweak
{
	public required TweakDefinition Tweak { get; init; }

	public required TweakState CurrentState { get; init; }

	public required IReadOnlyList<string> Descriptions { get; init; }

	public bool AlreadyApplied => CurrentState == TweakState.Applied;

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PlannedTweak(PlannedTweak original)
	{
		Tweak = original.Tweak;
		CurrentState = original.CurrentState;
		Descriptions = original.Descriptions;
	}

	public PlannedTweak()
	{
	}
}

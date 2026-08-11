using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Optimal.Core.Manifest;

namespace Optimal.Core.Execution;

public sealed record ExecutionPlan
{
	public required IReadOnlyList<PlannedTweak> Tweaks { get; init; }

	public required IReadOnlyList<ExcludedTweak> Excluded { get; init; }

	public bool RebootRequired => Tweaks.Any((PlannedTweak t) => t.Tweak.Reboot && !t.AlreadyApplied);

	public int ChangeCount => Tweaks.Count((PlannedTweak t) => !t.AlreadyApplied);

	public bool HasAggressiveTweaks => Tweaks.Any((PlannedTweak t) => t.Tweak.Tier == TweakTier.Aggressive);

	public IReadOnlyList<PlannedTweak> AggressiveTweaks => Tweaks.Where((PlannedTweak t) => t.Tweak.Tier == TweakTier.Aggressive).ToList();

	public int OperationCount => Tweaks.Sum((PlannedTweak t) => t.Tweak.Apply.Count);

	public static ExecutionPlan Empty { get; } = new ExecutionPlan
	{
		Tweaks = Array.Empty<PlannedTweak>(),
		Excluded = Array.Empty<ExcludedTweak>()
	};

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ExecutionPlan(ExecutionPlan original)
	{
		Tweaks = original.Tweaks;
		Excluded = original.Excluded;
	}

	public ExecutionPlan()
	{
	}
}

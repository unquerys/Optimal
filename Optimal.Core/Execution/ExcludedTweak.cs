using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Optimal.Core.Manifest;

namespace Optimal.Core.Execution;

public sealed record ExcludedTweak
{
	public required TweakDefinition Tweak { get; init; }

	public required ExclusionReason Reason { get; init; }

	public required string Explanation { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ExcludedTweak(ExcludedTweak original)
	{
		Tweak = original.Tweak;
		Reason = original.Reason;
		Explanation = original.Explanation;
	}

	public ExcludedTweak()
	{
	}
}

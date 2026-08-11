using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Optimal.Core.Safety;

public sealed class RunRecord
{
	[JsonPropertyName("runId")]
	public required string RunId { get; init; }

	[JsonPropertyName("startedUtc")]
	public required DateTimeOffset StartedUtc { get; init; }

	[JsonPropertyName("completedUtc")]
	public DateTimeOffset? CompletedUtc { get; set; }

	[JsonPropertyName("mode")]
	public required ExecutionMode Mode { get; init; }

	[JsonPropertyName("appVersion")]
	public required string AppVersion { get; init; }

	[JsonPropertyName("presetName")]
	public string? PresetName { get; set; }

	[JsonPropertyName("restorePointStatus")]
	public RestorePointStatus? RestorePointStatus { get; set; }

	[JsonPropertyName("registryBackupDirectory")]
	public string? RegistryBackupDirectory { get; set; }

	[JsonPropertyName("revertsRunId")]
	public string? RevertsRunId { get; set; }

	[JsonPropertyName("tweaks")]
	public List<TweakRunRecord> Tweaks { get; init; } = new List<TweakRunRecord>();

	[JsonIgnore]
	public bool RebootRequired => Tweaks.Any((TweakRunRecord t) => t.RebootRequired && t.Outcome == StepOutcome.Applied);

	[JsonIgnore]
	public int AppliedCount => Tweaks.Count((TweakRunRecord t) => t.Outcome == StepOutcome.Applied);

	[JsonIgnore]
	public int SkippedCount => Tweaks.Count(delegate(TweakRunRecord t)
	{
		StepOutcome outcome = t.Outcome;
		return (outcome == StepOutcome.Skipped || outcome == StepOutcome.NotApplicable) ? true : false;
	});

	[JsonIgnore]
	public int FailedCount => Tweaks.Count((TweakRunRecord t) => t.Outcome == StepOutcome.Failed);

	[JsonIgnore]
	public bool CanRevert
	{
		get
		{
			if (Mode == ExecutionMode.Apply)
			{
				return Tweaks.Any((TweakRunRecord t) => t.Outcome == StepOutcome.Applied);
			}
			return false;
		}
	}
}

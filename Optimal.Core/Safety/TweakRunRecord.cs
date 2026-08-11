using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Optimal.Core.Safety;

public sealed class TweakRunRecord
{
	[JsonPropertyName("tweakId")]
	public required string TweakId { get; init; }

	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("outcome")]
	public StepOutcome Outcome { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }

	[JsonPropertyName("rebootRequired")]
	public bool RebootRequired { get; init; }

	[JsonPropertyName("operations")]
	public List<OperationRunRecord> Operations { get; init; } = new List<OperationRunRecord>();
}

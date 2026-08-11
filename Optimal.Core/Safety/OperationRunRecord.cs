using System.Collections.Generic;
using System.Text.Json.Serialization;
using Optimal.Core.Operations;

namespace Optimal.Core.Safety;

public sealed class OperationRunRecord
{
	[JsonPropertyName("type")]
	public required string Type { get; init; }

	[JsonPropertyName("describe")]
	public required string Describe { get; init; }

	[JsonPropertyName("outcome")]
	public StepOutcome Outcome { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }

	[JsonPropertyName("backups")]
	public List<BackupEntry> Backups { get; init; } = new List<BackupEntry>();
}

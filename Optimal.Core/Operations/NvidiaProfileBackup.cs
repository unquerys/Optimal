using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record NvidiaProfileBackup : BackupEntry
{
	[JsonPropertyName("inspectorPath")]
	public required string InspectorPath { get; init; }

	[JsonPropertyName("backupPath")]
	public string? BackupPath { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private NvidiaProfileBackup(NvidiaProfileBackup original)
		: base(original)
	{
		InspectorPath = original.InspectorPath;
		BackupPath = original.BackupPath;
	}

	public NvidiaProfileBackup()
	{
	}
}

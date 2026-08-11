using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record PowerSchemeBackup : BackupEntry
{
	[JsonPropertyName("activeGuid")]
	public required string ActiveGuid { get; init; }

	[JsonPropertyName("friendlyName")]
	public string? FriendlyName { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PowerSchemeBackup(PowerSchemeBackup original)
		: base(original)
	{
		ActiveGuid = original.ActiveGuid;
		FriendlyName = original.FriendlyName;
	}

	public PowerSchemeBackup()
	{
	}
}

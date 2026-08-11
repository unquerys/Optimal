using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record PowerSchemeCreatedBackup : BackupEntry
{
	[JsonPropertyName("schemeGuid")]
	public required string SchemeGuid { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PowerSchemeCreatedBackup(PowerSchemeCreatedBackup original)
		: base(original)
	{
		SchemeGuid = original.SchemeGuid;
	}

	public PowerSchemeCreatedBackup()
	{
	}
}

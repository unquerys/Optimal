using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record PackageStateBackup : BackupEntry
{
	[JsonPropertyName("packageId")]
	public required string PackageId { get; init; }

	[JsonPropertyName("wasInstalled")]
	public required bool WasInstalled { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PackageStateBackup(PackageStateBackup original)
		: base(original)
	{
		PackageId = original.PackageId;
		WasInstalled = original.WasInstalled;
	}

	public PackageStateBackup()
	{
	}
}

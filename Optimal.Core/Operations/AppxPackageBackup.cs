using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record AppxPackageBackup : BackupEntry
{
	[JsonPropertyName("packageName")]
	public required string PackageName { get; init; }

	[JsonPropertyName("wasInstalled")]
	public required bool WasInstalled { get; init; }

	[JsonPropertyName("manifestPath")]
	public string? ManifestPath { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private AppxPackageBackup(AppxPackageBackup original)
		: base(original)
	{
		PackageName = original.PackageName;
		WasInstalled = original.WasInstalled;
		ManifestPath = original.ManifestPath;
	}

	public AppxPackageBackup()
	{
	}
}

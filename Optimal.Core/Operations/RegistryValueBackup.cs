using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record RegistryValueBackup : BackupEntry
{
	[JsonPropertyName("hive")]
	public required string Hive { get; init; }

	[JsonPropertyName("subKey")]
	public required string SubKey { get; init; }

	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("view")]
	public int View { get; init; } = 64;

	[JsonPropertyName("existed")]
	public required bool Existed { get; init; }

	[JsonPropertyName("keyExisted")]
	public bool KeyExisted { get; init; } = true;

	[JsonPropertyName("valueType")]
	public string? ValueType { get; init; }

	[JsonPropertyName("data")]
	public string? Data { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private RegistryValueBackup(RegistryValueBackup original)
		: base(original)
	{
		Hive = original.Hive;
		SubKey = original.SubKey;
		Name = original.Name;
		View = original.View;
		Existed = original.Existed;
		KeyExisted = original.KeyExisted;
		ValueType = original.ValueType;
		Data = original.Data;
	}

	public RegistryValueBackup()
	{
	}
}

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

public sealed record PowerSettingBackup : BackupEntry
{
	[JsonPropertyName("schemeGuid")]
	public required string SchemeGuid { get; init; }

	[JsonPropertyName("subgroupGuid")]
	public required string SubgroupGuid { get; init; }

	[JsonPropertyName("settingGuid")]
	public required string SettingGuid { get; init; }

	[JsonPropertyName("acValue")]
	public uint? AcValue { get; init; }

	[JsonPropertyName("dcValue")]
	public uint? DcValue { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PowerSettingBackup(PowerSettingBackup original)
		: base(original)
	{
		SchemeGuid = original.SchemeGuid;
		SubgroupGuid = original.SubgroupGuid;
		SettingGuid = original.SettingGuid;
		AcValue = original.AcValue;
		DcValue = original.DcValue;
	}

	public PowerSettingBackup()
	{
	}
}

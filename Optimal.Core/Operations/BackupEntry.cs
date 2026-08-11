using System.Text.Json.Serialization;

namespace Optimal.Core.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RegistryValueBackup), "registryValue")]
[JsonDerivedType(typeof(PowerSchemeBackup), "powerScheme")]
[JsonDerivedType(typeof(PowerSettingBackup), "powerSetting")]
[JsonDerivedType(typeof(PowerSchemeCreatedBackup), "powerSchemeCreated")]
[JsonDerivedType(typeof(PackageStateBackup), "packageState")]
[JsonDerivedType(typeof(AppxPackageBackup), "appxPackage")]
[JsonDerivedType(typeof(NvidiaProfileBackup), "nvidiaProfile")]
public abstract record BackupEntry
{
	[JsonPropertyName("describe")]
	public string Describe { get; init; } = string.Empty;
}

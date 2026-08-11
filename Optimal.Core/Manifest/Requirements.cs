using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Manifest;

public sealed record Requirements
{
	[JsonPropertyName("minBuild")]
	public int? MinBuild { get; init; }

	[JsonPropertyName("maxBuild")]
	public int? MaxBuild { get; init; }

	[JsonPropertyName("editions")]
	public IReadOnlyList<string>? Editions { get; init; }

	[JsonPropertyName("deviceKind")]
	public DeviceKind DeviceKind { get; init; }

	[JsonPropertyName("gpuVendor")]
	public GpuVendor GpuVendor { get; init; }

	[JsonPropertyName("capabilities")]
	public IReadOnlyList<string>? Capabilities { get; init; }

	public static Requirements None { get; } = new Requirements();

	[CompilerGenerated]
	private Requirements(Requirements original)
	{
		MinBuild = original.MinBuild;
		MaxBuild = original.MaxBuild;
		Editions = original.Editions;
		DeviceKind = original.DeviceKind;
		GpuVendor = original.GpuVendor;
		Capabilities = original.Capabilities;
	}

	public Requirements()
	{
	}
}

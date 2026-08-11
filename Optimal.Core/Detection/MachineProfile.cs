using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Optimal.Core.Manifest;

namespace Optimal.Core.Detection;

public sealed record MachineProfile
{
	public required string OsName { get; init; }

	public required string Edition { get; init; }

	public required int Build { get; init; }

	public string? DisplayVersion { get; init; }

	public required bool IsWindows11 { get; init; }

	public required DeviceKind DeviceKind { get; init; }

	public bool IsLaptop => DeviceKind == DeviceKind.Laptop;

	public required string CpuName { get; init; }

	public required int CpuPhysicalCores { get; init; }

	public required int CpuLogicalCores { get; init; }

	public required string GpuName { get; init; }

	public required GpuVendor GpuVendor { get; init; }

	public required int RamGigabytes { get; init; }

	public required StorageKind SystemDriveKind { get; init; }

	public required bool IsElevated { get; init; }

	public required IReadOnlySet<string> Capabilities { get; init; }

	public static MachineProfile Unknown { get; } = new MachineProfile
	{
		OsName = "Unknown Windows",
		Edition = "Unknown",
		Build = 0,
		IsWindows11 = false,
		DeviceKind = DeviceKind.Unknown,
		CpuName = "Unknown",
		CpuPhysicalCores = 0,
		CpuLogicalCores = 0,
		GpuName = "Unknown",
		GpuVendor = GpuVendor.Any,
		RamGigabytes = 0,
		SystemDriveKind = StorageKind.Unknown,
		IsElevated = false,
		Capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	};

	public bool HasCapability(string capability)
	{
		return Capabilities.Contains(capability);
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private MachineProfile(MachineProfile original)
	{
		OsName = original.OsName;
		Edition = original.Edition;
		Build = original.Build;
		DisplayVersion = original.DisplayVersion;
		IsWindows11 = original.IsWindows11;
		DeviceKind = original.DeviceKind;
		CpuName = original.CpuName;
		CpuPhysicalCores = original.CpuPhysicalCores;
		CpuLogicalCores = original.CpuLogicalCores;
		GpuName = original.GpuName;
		GpuVendor = original.GpuVendor;
		RamGigabytes = original.RamGigabytes;
		SystemDriveKind = original.SystemDriveKind;
		IsElevated = original.IsElevated;
		Capabilities = original.Capabilities;
	}

	public MachineProfile()
	{
	}
}

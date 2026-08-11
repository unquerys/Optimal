using System;
using System.Collections.Generic;
using Optimal.Core.Detection;
using Optimal.Core.Manifest;
using Xunit;

namespace Optimal.Tests;

public sealed class HardwareAdvisorTests
{
	[Fact]
	public void Desktop_3060Ti_And_I7_UsesCompetitiveProfile()
	{
		HardwareRecommendation hardwareRecommendation = HardwareAdvisor.Recommend(Profile("Intel(R) Core(TM) i7-12700K", "NVIDIA GeForce RTX 3060 Ti", DeviceKind.Desktop, 12));
		Assert.Equal("gaming.nvidia.competitive", hardwareRecommendation.ProfileId);
		Assert.True(hardwareRecommendation.CanApplyAutomatically);
	}

	[Fact]
	public void Laptop_4060_UsesBalancedProfile()
	{
		HardwareRecommendation hardwareRecommendation = HardwareAdvisor.Recommend(Profile("AMD Ryzen 7 7840HS", "NVIDIA GeForce RTX 4060 Laptop GPU", DeviceKind.Laptop, 8));
		Assert.Equal("gaming.nvidia.balanced-latency", hardwareRecommendation.ProfileId);
	}

	[Fact]
	public void AmdGpu_DoesNotOfferNvidiaProfile()
	{
		Assert.False(HardwareAdvisor.Recommend(Profile("AMD Ryzen 5 7600", "AMD Radeon RX 7800 XT", DeviceKind.Desktop, 6)with
		{
			GpuVendor = GpuVendor.Amd
		}).CanApplyAutomatically);
	}

	private static MachineProfile Profile(string cpu, string gpu, DeviceKind device, int cores)
	{
		return new MachineProfile
		{
			OsName = "Windows 11",
			Edition = "Pro",
			Build = 26100,
			IsWindows11 = true,
			DeviceKind = device,
			CpuName = cpu,
			CpuPhysicalCores = cores,
			CpuLogicalCores = cores * 2,
			GpuName = gpu,
			GpuVendor = GpuVendor.Nvidia,
			RamGigabytes = 32,
			SystemDriveKind = StorageKind.Nvme,
			IsElevated = true,
			Capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hags" }
		};
	}
}

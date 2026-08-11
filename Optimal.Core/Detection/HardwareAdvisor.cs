using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Optimal.Core.Manifest;

namespace Optimal.Core.Detection;

public static class HardwareAdvisor
{
	public static IReadOnlyList<string> PopularGpuFamilies { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[18]
	{
		"RTX 3060", "RTX 4060", "RTX 3050", "RTX 5070", "GTX 1650", "RTX 5060", "RTX 4060 Ti", "RTX 3060 Ti", "RTX 3070", "RTX 4070",
		"RTX 2060", "RTX 3080", "RX 6600", "RX 6700", "RX 7800", "RX 9070", "Intel Arc A", "Intel Arc B"
	});

	public static HardwareRecommendation Recommend(MachineProfile profile)
	{
		if (profile.GpuVendor != GpuVendor.Nvidia)
		{
			return new HardwareRecommendation(string.Empty, $"{profile.GpuVendor} guided setup", "HARDWARE MATCH", "Optimal detected a non-NVIDIA graphics adapter. NVIDIA driver profiles are hidden; Windows and vendor-neutral gaming tweaks remain available.", CanApplyAutomatically: false);
		}
		int num = ClassifyNvidiaGpu(profile.GpuName);
		int num2 = ClassifyCpu(profile.CpuName, profile.CpuPhysicalCores);
		if (profile.IsLaptop || profile.GpuName.Contains("Laptop", StringComparison.OrdinalIgnoreCase) || num == 1 || num2 == 1)
		{
			return new HardwareRecommendation("gaming.nvidia.balanced-latency", "Balanced latency", "THERMAL-SAFE MATCH", $"Matched {Shorten(profile.GpuName)} with {Shorten(profile.CpuName)}. Balanced latency avoids a permanent maximum-power policy on mobile or entry-level hardware.", CanApplyAutomatically: true);
		}
		return new HardwareRecommendation("gaming.nvidia.competitive", "Competitive latency", (num >= 3 && num2 >= 3) ? "HIGH-CONFIDENCE MATCH" : "BALANCED MATCH", $"Matched {Shorten(profile.GpuName)} with {Shorten(profile.CpuName)} ({profile.CpuPhysicalCores} cores). This combination has enough CPU/GPU headroom for the competitive profile; game-level Reflex still takes priority when available.", CanApplyAutomatically: true);
	}

	internal static int ClassifyNvidiaGpu(string name)
	{
		Match match = GpuNumberRegex().Match(name);
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return 1;
		}
		int num = result / 100;
		int num2 = result % 100;
		if (num >= 20 && num2 >= 70)
		{
			return 3;
		}
		if (num >= 20 && num2 >= 60)
		{
			return 2;
		}
		if (num >= 40 && num2 >= 50)
		{
			return 2;
		}
		return 1;
	}

	internal static int ClassifyCpu(string name, int physicalCores)
	{
		if (name.Contains("i9", StringComparison.OrdinalIgnoreCase) || name.Contains("Ryzen 9", StringComparison.OrdinalIgnoreCase))
		{
			return 3;
		}
		if (name.Contains("i7", StringComparison.OrdinalIgnoreCase) || name.Contains("Ryzen 7", StringComparison.OrdinalIgnoreCase) || physicalCores >= 8)
		{
			return 3;
		}
		if (name.Contains("i5", StringComparison.OrdinalIgnoreCase) || name.Contains("Ryzen 5", StringComparison.OrdinalIgnoreCase) || physicalCores >= 6)
		{
			return 2;
		}
		return 1;
	}

	private static string Shorten(string value)
	{
		if (value.Length > 42)
		{
			return value.Substring(0, 39) + "...";
		}
		return value;
	}

	private static Regex GpuNumberRegex() => new("(?:RTX|GTX)\\s*(\\d{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

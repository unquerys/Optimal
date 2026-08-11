using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Optimal.Core.Manifest;

namespace Optimal.Core.Detection;

[SupportedOSPlatform("windows")]
public sealed class MachineProfiler
{
	private const string CurrentVersionKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";

	private readonly ILogger<MachineProfiler> _logger;

	public MachineProfiler(ILogger<MachineProfiler> logger)
	{
		_logger = logger;
	}

	public async Task<MachineProfile> ProfileAsync(IProgress<ProbeProgress>? progress, CancellationToken cancellationToken)
	{
		(string Name, Action Probe)[] stages = new(string, Action)[8];
		HashSet<string> capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string osName = "Windows";
		string edition = "Unknown";
		string displayVersion = string.Empty;
		int build = 0;
		DeviceKind deviceKind = DeviceKind.Unknown;
		string cpuName = "Unknown";
		string gpuName = "Unknown";
		int physicalCores = 0;
		int logicalCores = Environment.ProcessorCount;
		int ramGb = 0;
		GpuVendor gpuVendor = GpuVendor.Any;
		StorageKind storage = StorageKind.Unknown;
		bool elevated = false;
		stages[0] = (Name: "Reading Windows version", Probe: delegate
		{
			(osName, edition, build, displayVersion) = ProbeWindows();
		});
		stages[1] = (Name: "Checking permissions", Probe: delegate
		{
			elevated = ProbeElevation();
		});
		stages[2] = (Name: "Detecting processor", Probe: delegate
		{
			(cpuName, physicalCores, logicalCores) = ProbeCpu();
		});
		stages[3] = (Name: "Detecting memory", Probe: delegate
		{
			ramGb = ProbeMemory();
		});
		stages[4] = (Name: "Detecting graphics", Probe: delegate
		{
			(gpuName, gpuVendor) = ProbeGpu();
		});
		stages[5] = (Name: "Detecting chassis", Probe: delegate
		{
			deviceKind = ProbeDeviceKind();
		});
		stages[6] = (Name: "Detecting storage", Probe: delegate
		{
			storage = ProbeSystemDriveKind();
		});
		stages[7] = (Name: "Checking feature support", Probe: delegate
		{
			ProbeCapabilities(capabilities);
		});
		for (int i = 0; i < stages.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var (name, probe) = stages[i];
			progress?.Report(new ProbeProgress(name, i, stages.Length));
			await Task.Run(delegate
			{
				try
				{
					probe();
				}
				catch (Exception exception)
				{
					_logger.LogWarning(exception, "Probe '{Stage}' failed, continuing with defaults.", name);
				}
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		progress?.Report(new ProbeProgress("Done", stages.Length, stages.Length));
		if (gpuVendor == GpuVendor.Nvidia || gpuVendor == GpuVendor.Amd || gpuVendor == GpuVendor.Intel)
		{
			capabilities.Add("gpu:" + gpuVendor.ToString().ToLowerInvariant());
		}
		DeviceKind deviceKind2 = deviceKind;
		if ((uint)(deviceKind2 - 1) <= 1u)
		{
			capabilities.Add("device:" + deviceKind.ToString().ToLowerInvariant());
		}
		if (build >= 22000)
		{
			capabilities.Add("os:windows11");
		}
		if (storage != StorageKind.Unknown)
		{
			capabilities.Add("storage:" + storage.ToString().ToLowerInvariant());
		}
		MachineProfile machineProfile = new MachineProfile
		{
			OsName = osName,
			Edition = edition,
			Build = build,
			DisplayVersion = (string.IsNullOrWhiteSpace(displayVersion) ? null : displayVersion),
			IsWindows11 = (build >= 22000),
			DeviceKind = deviceKind,
			CpuName = cpuName,
			CpuPhysicalCores = physicalCores,
			CpuLogicalCores = logicalCores,
			GpuName = gpuName,
			GpuVendor = gpuVendor,
			RamGigabytes = ramGb,
			SystemDriveKind = storage,
			IsElevated = elevated,
			Capabilities = capabilities
		};
		_logger.LogInformation("Detected {OsName} build {Build} on {Cpu} with {Gpu}, {Ram} GB, {DeviceKind}.", machineProfile.OsName, machineProfile.Build, machineProfile.CpuName, machineProfile.GpuName, machineProfile.RamGigabytes, machineProfile.DeviceKind);
		return machineProfile;
	}

	private (string Name, string Edition, int Build, string DisplayVersion) ProbeWindows()
	{
		using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", writable: false);
		string text = (registryKey?.GetValue("ProductName") as string) ?? "Windows";
		string item = (registryKey?.GetValue("DisplayVersion") as string) ?? string.Empty;
		string item2 = NormalizeEdition(registryKey?.GetValue("EditionID") as string);
		int build = Environment.OSVersion.Version.Build;
		if (build >= 22000 && text.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
		}
		return (Name: text, Edition: item2, Build: build, DisplayVersion: item);
	}

	private static bool ProbeElevation()
	{
		using WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent();
		return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
	}

	private (string Name, int Physical, int Logical) ProbeCpu()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		ManagementObjectSearcher val = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
		try
		{
			List<string> list = new List<string>();
			int num = 0;
			int num2 = 0;
			foreach (ManagementObject item in ((IEnumerable)val.Get()).Cast<ManagementObject>())
			{
				ManagementObject val2 = item;
				try
				{
					string text = (((ManagementBaseObject)item)["Name"] as string)?.Trim();
					if (!string.IsNullOrWhiteSpace(text))
					{
						list.Add(text);
					}
					num += ToInt(((ManagementBaseObject)item)["NumberOfCores"]);
					num2 += ToInt(((ManagementBaseObject)item)["NumberOfLogicalProcessors"]);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			return (Name: list.Count switch
			{
				0 => "Unknown", 
				1 => list[0], 
				_ => (list.Distinct<string>(StringComparer.OrdinalIgnoreCase).Count() != 1) ? string.Join(" + ", list) : $"{list[0]} ({list.Count} sockets)", 
			}, Physical: num, Logical: (num2 == 0) ? Environment.ProcessorCount : num2);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private int ProbeMemory()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		ManagementObjectSearcher val = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
		try
		{
			foreach (ManagementObject item in ((IEnumerable)val.Get()).Cast<ManagementObject>())
			{
				ManagementObject val2 = item;
				try
				{
					if (((ManagementBaseObject)item)["TotalPhysicalMemory"] != null && ulong.TryParse(((ManagementBaseObject)item)["TotalPhysicalMemory"].ToString(), out var result))
					{
						return (int)Math.Round((double)result / 1024.0 / 1024.0 / 1024.0);
					}
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			return 0;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private (string Name, GpuVendor Vendor) ProbeGpu()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		ManagementObjectSearcher val = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility, AdapterRAM FROM Win32_VideoController");
		try
		{
			(string, GpuVendor, long) tuple = ("Unknown", GpuVendor.Any, -1L);
			foreach (ManagementObject item in ((IEnumerable)val.Get()).Cast<ManagementObject>())
			{
				ManagementObject val2 = item;
				try
				{
					string text = (((ManagementBaseObject)item)["Name"] as string)?.Trim() ?? "Unknown";
					GpuVendor gpuVendor = ClassifyVendor((((ManagementBaseObject)item)["AdapterCompatibility"] as string) ?? text);
					long result;
					long num = ((((ManagementBaseObject)item)["AdapterRAM"] != null && long.TryParse(((ManagementBaseObject)item)["AdapterRAM"].ToString(), out result)) ? result : 0);
					bool flag = (uint)(gpuVendor - 1) <= 1u;
					long num2 = (flag ? (num + 4611686018427387903L) : num);
					if (num2 > tuple.Item3)
					{
						tuple = (text, gpuVendor, num2);
					}
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			return (Name: tuple.Item1, Vendor: tuple.Item2);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static GpuVendor ClassifyVendor(string text)
	{
		if (text.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
		{
			return GpuVendor.Nvidia;
		}
		if (text.Contains("AMD", StringComparison.OrdinalIgnoreCase) || text.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) || text.Contains("ATI", StringComparison.OrdinalIgnoreCase))
		{
			return GpuVendor.Amd;
		}
		if (!text.Contains("Intel", StringComparison.OrdinalIgnoreCase))
		{
			return GpuVendor.Any;
		}
		return GpuVendor.Intel;
	}

	private DeviceKind ProbeDeviceKind()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		ManagementObjectSearcher val = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery");
		try
		{
			if (((IEnumerable)val.Get()).Cast<ManagementObject>().Any())
			{
				return DeviceKind.Laptop;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		ManagementObjectSearcher val2 = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
		try
		{
			foreach (ManagementObject item in ((IEnumerable)val2.Get()).Cast<ManagementObject>())
			{
				ManagementObject val3 = item;
				try
				{
					if (!(((ManagementBaseObject)item)["ChassisTypes"] is Array source) || !source.Cast<object>().Select(ToInt).Any((int t) => ((uint)(t - 8) <= 3u || t == 14 || (uint)(t - 30) <= 2u) ? true : false))
					{
						continue;
					}
					return DeviceKind.Laptop;
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			return DeviceKind.Desktop;
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private StorageKind ProbeSystemDriveKind()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected O, but got Unknown
		//IL_00c9: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		string text = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
		if (string.IsNullOrWhiteSpace(text))
		{
			return StorageKind.Unknown;
		}
		ManagementObjectSearcher val = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + text + "'} WHERE AssocClass=Win32_LogicalDiskToPartition");
		try
		{
			int? value = ((IEnumerable)val.Get()).Cast<ManagementObject>().Select(delegate(ManagementObject item)
			{
				try
				{
					return ToInt(((ManagementBaseObject)item)["DiskIndex"]);
				}
				finally
				{
					((IDisposable)item)?.Dispose();
				}
			}).Cast<int?>()
				.FirstOrDefault();
			if (!value.HasValue)
			{
				return StorageKind.Unknown;
			}
			ManagementObjectSearcher val2 = new ManagementObjectSearcher(new ManagementScope("\\\\.\\root\\Microsoft\\Windows\\Storage"), new ObjectQuery($"SELECT MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId = '{value}'"));
			try
			{
				using (IEnumerator<ManagementObject> enumerator = ((IEnumerable)val2.Get()).Cast<ManagementObject>().GetEnumerator())
				{
					if (enumerator.MoveNext())
					{
						ManagementObject current = enumerator.Current;
						ManagementObject val3 = current;
						try
						{
							return ClassifyStorage(ToInt(((ManagementBaseObject)current)["MediaType"]), ToInt(((ManagementBaseObject)current)["BusType"]));
						}
						finally
						{
							((IDisposable)val3)?.Dispose();
						}
					}
				}
				return StorageKind.Unknown;
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void ProbeCapabilities(HashSet<string> capabilities)
	{
		using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", writable: false);
		if (registryKey?.GetValue("HwSchSupported") is int num && num == 1)
		{
			capabilities.Add("hags");
		}
		if (Environment.Is64BitOperatingSystem)
		{
			capabilities.Add("x64");
		}
	}

	private static int ToInt(object? value)
	{
		if (value == null || !int.TryParse(value.ToString(), out var result))
		{
			return 0;
		}
		return result;
	}

	internal static StorageKind ClassifyStorage(int mediaType, int busType)
	{
		if (busType == 17)
		{
			return StorageKind.Nvme;
		}
		return mediaType switch
		{
			4 => StorageKind.Ssd, 
			3 => StorageKind.Hdd, 
			_ => StorageKind.Unknown, 
		};
	}

	internal static string NormalizeEdition(string? editionId)
	{
		if (string.IsNullOrWhiteSpace(editionId))
		{
			return "Unknown";
		}
		if (editionId.Contains("Enterprise", StringComparison.OrdinalIgnoreCase))
		{
			return "Enterprise";
		}
		if (editionId.Contains("Education", StringComparison.OrdinalIgnoreCase))
		{
			return "Education";
		}
		if (editionId.Contains("Professional", StringComparison.OrdinalIgnoreCase) || editionId.Equals("Pro", StringComparison.OrdinalIgnoreCase))
		{
			return "Pro";
		}
		if (!editionId.Contains("Core", StringComparison.OrdinalIgnoreCase))
		{
			return editionId;
		}
		return "Home";
	}
}

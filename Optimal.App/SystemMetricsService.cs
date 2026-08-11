using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace Optimal.App;

internal sealed class SystemMetricsService
{
	public async Task<SystemMetricSample> SampleAsync(CancellationToken cancellationToken)
	{
		return await Task.Run(async delegate
		{
			double cpu = QuerySingle("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'", "PercentProcessorTime");
			double diskActivity = QuerySingle("SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'", "PercentDiskTime");
			var (memory, memoryText) = QueryMemory();
			var (storage, storageText) = QueryStorage();
			var (num, gpuTemperature) = await QueryNvidiaAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new SystemMetricSample(Clamp(cpu), Clamp(memory), Clamp(storage), Clamp(diskActivity), (!num.HasValue) ? ((double?)null) : new double?(Clamp(num.Value)), gpuTemperature, storageText + " · " + memoryText);
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static double QuerySingle(string query, string property)
	{
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(query);
			using IEnumerator<ManagementObject> enumerator = managementObjectSearcher.Get().Cast<ManagementObject>().GetEnumerator();
			if (enumerator.MoveNext())
			{
				ManagementObject current = enumerator.Current;
				using (current)
				{
					double result;
					return double.TryParse(current[property]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0.0;
				}
			}
		}
		catch
		{
		}
		return 0.0;
	}

	private static (double Percent, string Text) QueryMemory()
	{
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
			using IEnumerator<ManagementObject> enumerator = managementObjectSearcher.Get().Cast<ManagementObject>().GetEnumerator();
			if (enumerator.MoveNext())
			{
				ManagementObject current = enumerator.Current;
				using (current)
				{
					double num = Convert.ToDouble(current["TotalVisibleMemorySize"], CultureInfo.InvariantCulture);
					double num2 = Convert.ToDouble(current["FreePhysicalMemory"], CultureInfo.InvariantCulture);
					double value = (num - num2) / 1024.0 / 1024.0;
					return (Percent: (num <= 0.0) ? 0.0 : ((num - num2) / num * 100.0), Text: $"{value:0.0} GB RAM used");
				}
			}
		}
		catch
		{
		}
		return (Percent: 0.0, Text: "RAM unavailable");
	}

	private static (double Percent, string Text) QueryStorage()
	{
		try
		{
			DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
			long num = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
			return (Percent: (double)num / (double)driveInfo.TotalSize * 100.0, Text: $"{(double)num / 1024.0 / 1024.0 / 1024.0:0} of {(double)driveInfo.TotalSize / 1024.0 / 1024.0 / 1024.0:0} GB");
		}
		catch
		{
			return (Percent: 0.0, Text: "Storage unavailable");
		}
	}

	private static async Task<(double? Gpu, double? Temperature)> QueryNvidiaAsync(CancellationToken cancellationToken)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
		string fileName = (File.Exists(text) ? text : "nvidia-smi.exe");
		try
		{
			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo(fileName, "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};
			process.Start();
			string output = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(3L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string[] array = output?.Split(',', StringSplitOptions.TrimEntries);
			double result;
			double result2;
			return (array != null && array.Length >= 2 && double.TryParse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture, out result) && double.TryParse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture, out result2)) ? (Gpu: result, Temperature: result2) : (Gpu: (double?)null, Temperature: (double?)null);
		}
		catch
		{
			return (Gpu: null, Temperature: null);
		}
	}

	private static double Clamp(double value)
	{
		return Math.Clamp(value, 0.0, 100.0);
	}
}

using System;
using System.Management;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Optimal.Core.Safety;

[SupportedOSPlatform("windows")]
public sealed class SystemRestorePointService
{
	private const string SystemRestoreKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore";

	private const string FrequencyValue = "SystemRestorePointCreationFrequency";

	private readonly ILogger<SystemRestorePointService> _logger;

	public SystemRestorePointService(ILogger<SystemRestorePointService> logger)
	{
		_logger = logger;
	}

	public async Task<RestorePointResult> CreateAsync(string description, CancellationToken cancellationToken)
	{
		return await Task.Run(() => Create(description), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private RestorePointResult Create(string description)
	{
		//IL_01a0: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0037: Expected O, but got Unknown
		//IL_0037: Expected O, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		int? previous = null;
		bool flag = false;
		try
		{
			previous = ReadFrequency();
			flag = TryWriteFrequency(0);
			ManagementClass val = new ManagementClass(new ManagementScope("\\\\.\\root\\default"), new ManagementPath("SystemRestore"), new ObjectGetOptions());
			try
			{
				ManagementBaseObject methodParameters = ((ManagementObject)val).GetMethodParameters("CreateRestorePoint");
				try
				{
					methodParameters["Description"] = description;
					methodParameters["RestorePointType"] = 12;
					methodParameters["EventType"] = 100;
					ManagementBaseObject val2 = ((ManagementObject)val).InvokeMethod("CreateRestorePoint", methodParameters, (InvokeMethodOptions)null);
					try
					{
						uint num = Convert.ToUInt32(val2["ReturnValue"]);
						switch (num)
						{
						case 0u:
							_logger.LogInformation("Created system restore point: {Description}", description);
							return new RestorePointResult(RestorePointStatus.Created, "Created restore point '" + description + "'.");
						case 1058u:
							_logger.LogWarning("System Protection is disabled, no restore point was created.");
							return new RestorePointResult(RestorePointStatus.ProtectionDisabled, "System Protection is turned off for this drive, so Windows could not create a restore point. You can turn it on in System Properties, Protection Settings.");
						default:
							_logger.LogWarning("CreateRestorePoint returned {ReturnValue}.", num);
							return new RestorePointResult(RestorePointStatus.Failed, $"Windows declined to create a restore point (code {num}).");
						}
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)methodParameters)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (ManagementException ex)
		{
			ManagementException ex2 = ex;
			_logger.LogWarning((Exception?)(object)ex2, "WMI refused the restore point request.");
			return new RestorePointResult(RestorePointStatus.Failed, "Could not create a restore point: " + ((Exception)(object)ex2).Message);
		}
		catch (UnauthorizedAccessException exception)
		{
			_logger.LogWarning(exception, "Not permitted to create a restore point.");
			return new RestorePointResult(RestorePointStatus.Failed, "Creating a restore point requires administrator rights.");
		}
		finally
		{
			if (flag)
			{
				RestoreFrequency(previous);
			}
		}
	}

	private int? ReadFrequency()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore", writable: false);
			return registryKey?.GetValue("SystemRestorePointCreationFrequency") as int?;
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Could not read the restore point frequency setting.");
			return null;
		}
	}

	private bool TryWriteFrequency(int value)
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore", writable: true);
			registryKey?.SetValue("SystemRestorePointCreationFrequency", value, RegistryValueKind.DWord);
			return registryKey != null;
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Could not relax the restore point frequency setting.");
			return false;
		}
	}

	private void RestoreFrequency(int? previous)
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore", writable: true);
			if (registryKey != null)
			{
				if (previous.HasValue)
				{
					int valueOrDefault = previous.GetValueOrDefault();
					registryKey.SetValue("SystemRestorePointCreationFrequency", valueOrDefault, RegistryValueKind.DWord);
				}
				else
				{
					registryKey.DeleteValue("SystemRestorePointCreationFrequency", throwOnMissingValue: false);
				}
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Could not restore the original restore point frequency setting.");
		}
	}
}

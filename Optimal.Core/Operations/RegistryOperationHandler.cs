using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class RegistryOperationHandler : IOperationHandler, IBackupRestorer
{
	private const string ActionSet = "set";

	private const string ActionDelete = "delete";

	private const string ActionDeleteKey = "deletekey";

	public string Type => "registry";

	public void Validate(OperationSpec spec)
	{
		string text = ReadAction(spec);
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), ReadView(spec));
		if (string.IsNullOrEmpty(registryPath.SubKey))
		{
			throw new ManifestValidationException("Registry operation targets the root of " + registryPath.Hive + ", which is never correct.");
		}
		switch (text)
		{
		case "set":
		{
			RegistryValueKind kind = RegistryValueCodec.ParseKind(spec.RequireString("valueType"));
			RegistryValueCodec.FromJson(spec.Require("value"), kind);
			spec.OptionalString("name");
			break;
		}
		case "delete":
			spec.RequireString("name");
			break;
		default:
			throw new ManifestValidationException("Unrecognised registry action '" + text + "'. Expected set, delete, or deleteKey.");
		case "deletekey":
			break;
		}
	}

	public string Describe(OperationSpec spec)
	{
		string text = ReadAction(spec);
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), ReadView(spec));
		string text2 = spec.OptionalString("name") ?? "(default)";
		if (!(text == "delete"))
		{
			if (!(text == "deletekey"))
			{
				return $"Set {registryPath.FullPath}\\{text2} = {spec.Require("value").GetRawText()} ({spec.RequireString("valueType")})";
			}
			return "Delete registry key " + registryPath.FullPath;
		}
		return "Delete registry value " + registryPath.FullPath + "\\" + text2;
	}

	public Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		string text = ReadAction(spec);
		RegistryPath path = RegistryPath.Parse(spec.RequireString("path"), ReadView(spec));
		if (text == "deletekey")
		{
			return Task.FromResult((IReadOnlyList<BackupEntry>)Array.Empty<BackupEntry>());
		}
		string name = spec.OptionalString("name") ?? string.Empty;
		return Task.FromResult((IReadOnlyList<BackupEntry>)new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(CaptureValue(path, name)));
	}

	public Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		string text = ReadAction(spec);
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), ReadView(spec));
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping: {Description}", Describe(spec));
			return Task.CompletedTask;
		}
		switch (text)
		{
		case "set":
		{
			string text3 = spec.OptionalString("name") ?? string.Empty;
			RegistryValueKind registryValueKind = RegistryValueCodec.ParseKind(spec.RequireString("valueType"));
			object value = RegistryValueCodec.FromJson(spec.Require("value"), registryValueKind);
			using (RegistryKey registryKey4 = registryPath.OpenBaseKey())
			{
				using RegistryKey registryKey5 = registryKey4.CreateSubKey(registryPath.SubKey, writable: true) ?? throw new InvalidOperationException("Could not open or create " + registryPath.FullPath + ".");
				registryKey5.SetValue(text3, value, registryValueKind);
				context.Logger.LogDebug("Set {Path}\\{Name}", registryPath.FullPath, text3);
			}
			break;
		}
		case "delete":
		{
			string text2 = spec.RequireString("name");
			using (RegistryKey registryKey2 = registryPath.OpenBaseKey())
			{
				using RegistryKey registryKey3 = registryKey2.OpenSubKey(registryPath.SubKey, writable: true);
				registryKey3?.DeleteValue(text2, throwOnMissingValue: false);
				context.Logger.LogDebug("Deleted {Path}\\{Name}", registryPath.FullPath, text2);
			}
			break;
		}
		case "deletekey":
		{
			using (RegistryKey registryKey = registryPath.OpenBaseKey())
			{
				registryKey.DeleteSubKeyTree(registryPath.SubKey, throwOnMissingSubKey: false);
				context.Logger.LogDebug("Deleted key {Path}", registryPath.FullPath);
			}
			break;
		}
		}
		return Task.CompletedTask;
	}

	public bool CanRestore(BackupEntry entry)
	{
		return entry is RegistryValueBackup;
	}

	public Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken)
	{
		RegistryValueBackup registryValueBackup = (RegistryValueBackup)entry;
		RegistryPath registryPath = RegistryPath.FromBackup(registryValueBackup);
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping restore of {Path}\\{Name}", registryPath.FullPath, registryValueBackup.Name);
			return Task.CompletedTask;
		}
		using RegistryKey registryKey = registryPath.OpenBaseKey();
		if (!registryValueBackup.Existed)
		{
			using (RegistryKey registryKey2 = registryKey.OpenSubKey(registryPath.SubKey, writable: true))
			{
				registryKey2?.DeleteValue(registryValueBackup.Name, throwOnMissingValue: false);
			}
			if (!registryValueBackup.KeyExisted)
			{
				DeleteKeyIfEmpty(registryKey, registryPath.SubKey, context.Logger);
			}
			return Task.CompletedTask;
		}
		RegistryValueKind registryValueKind = RegistryValueCodec.ParseKind(registryValueBackup.ValueType);
		object value = RegistryValueCodec.FromStorage(registryValueBackup.Data, registryValueKind);
		using RegistryKey registryKey3 = registryKey.CreateSubKey(registryPath.SubKey, writable: true) ?? throw new InvalidOperationException("Could not open or create " + registryPath.FullPath + " to restore.");
		registryKey3.SetValue(registryValueBackup.Name, value, registryValueKind);
		context.Logger.LogDebug("Restored {Path}\\{Name}", registryPath.FullPath, registryValueBackup.Name);
		return Task.CompletedTask;
	}

	internal static RegistryValueBackup CaptureValue(RegistryPath path, string name)
	{
		using RegistryKey registryKey = path.OpenBaseKey();
		using RegistryKey registryKey2 = registryKey.OpenSubKey(path.SubKey, writable: false);
		string text = (string.IsNullOrEmpty(name) ? "(default)" : name);
		if (registryKey2 == null)
		{
			return new RegistryValueBackup
			{
				Hive = path.Hive,
				SubKey = path.SubKey,
				Name = name,
				View = path.ViewBits,
				Existed = false,
				KeyExisted = false,
				Describe = path.FullPath + "\\" + text + " did not exist"
			};
		}
		object value = registryKey2.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
		if (value == null)
		{
			return new RegistryValueBackup
			{
				Hive = path.Hive,
				SubKey = path.SubKey,
				Name = name,
				View = path.ViewBits,
				Existed = false,
				KeyExisted = true,
				Describe = path.FullPath + "\\" + text + " did not exist"
			};
		}
		RegistryValueKind valueKind = registryKey2.GetValueKind(name);
		return new RegistryValueBackup
		{
			Hive = path.Hive,
			SubKey = path.SubKey,
			Name = name,
			View = path.ViewBits,
			Existed = true,
			KeyExisted = true,
			ValueType = RegistryValueCodec.KindToToken(valueKind),
			Data = RegistryValueCodec.ToStorage(value, valueKind),
			Describe = $"{path.FullPath}\\{text} was {RegistryValueCodec.ToStorage(value, valueKind)}"
		};
	}

	private static void DeleteKeyIfEmpty(RegistryKey baseKey, string subKey, ILogger logger)
	{
		using (RegistryKey registryKey = baseKey.OpenSubKey(subKey, writable: false))
		{
			if (registryKey == null)
			{
				return;
			}
			if (registryKey.ValueCount != 0 || registryKey.SubKeyCount != 0)
			{
				logger.LogDebug("Leaving {SubKey} in place, it is no longer empty.", subKey);
				return;
			}
		}
		baseKey.DeleteSubKey(subKey, throwOnMissingSubKey: false);
		logger.LogDebug("Removed empty key {SubKey} that we created.", subKey);
	}

	private static string ReadAction(OperationSpec spec)
	{
		return (spec.OptionalString("action") ?? "set").ToLowerInvariant();
	}

	private static int ReadView(OperationSpec spec)
	{
		return spec.OptionalInt("view", 64);
	}
}

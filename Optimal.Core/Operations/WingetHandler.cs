using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class WingetHandler : IOperationHandler, IConditionHandler, IBackupRestorer
{
	private readonly IProcessRunner _process;

	public string Type => "winget";

	private static Regex PackageIdRegex { get; } = new("^[A-Za-z0-9][A-Za-z0-9._+-]{1,127}$", RegexOptions.CultureInvariant);

	public WingetHandler(IProcessRunner process)
	{
		_process = process;
	}

	public void Validate(OperationSpec spec)
	{
		ValidatePackageId(spec.RequireString("packageId"));
		string text = spec.RequireString("action");
		if (!text.Equals("install", StringComparison.OrdinalIgnoreCase) && !text.Equals("uninstall", StringComparison.OrdinalIgnoreCase))
		{
			throw new ManifestValidationException("winget action must be install or uninstall.");
		}
	}

	public void Validate(ConditionSpec spec)
	{
		ValidatePackageId(spec.RequireString("packageId"));
		JsonValueKind valueKind = spec.Require("installed").ValueKind;
		if (valueKind - 5 > JsonValueKind.Object)
		{
			throw new ManifestValidationException("winget condition 'installed' must be true or false.");
		}
	}

	public string Describe(OperationSpec spec)
	{
		return spec.RequireString("action") + " WinGet package " + spec.RequireString("packageId");
	}

	public string Describe(ConditionSpec spec)
	{
		return "WinGet package " + spec.RequireString("packageId") + " is " + (spec.Require("installed").GetBoolean() ? "installed" : "not installed");
	}

	public async Task<bool> EvaluateAsync(ConditionSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		return await IsInstalledAsync(spec.RequireString("packageId"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false) == spec.Require("installed").GetBoolean();
	}

	public async Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		string packageId = spec.RequireString("packageId");
		bool flag = await IsInstalledAsync(packageId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new PackageStateBackup
		{
			PackageId = packageId,
			WasInstalled = flag,
			Describe = packageId + " was " + (flag ? "installed" : "not installed")
		});
	}

	public async Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping: {Description}", Describe(spec));
		}
		else
		{
			await SetInstalledAsync(spec.RequireString("packageId"), spec.RequireString("action").Equals("install", StringComparison.OrdinalIgnoreCase), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public bool CanRestore(BackupEntry entry)
	{
		return entry is PackageStateBackup;
	}

	public async Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken)
	{
		PackageStateBackup packageStateBackup = (PackageStateBackup)entry;
		if (!context.DryRun)
		{
			await SetInstalledAsync(packageStateBackup.PackageId, packageStateBackup.WasInstalled, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken)
	{
		try
		{
			return (await _process.RunAsync(ResolveWinget(), new _003C_003Ez__ReadOnlyArray<string>(new string[6] { "list", "--id", packageId, "-e", "--accept-source-agreements", "--disable-interactivity" }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).StandardOutput.Contains(packageId, StringComparison.OrdinalIgnoreCase);
		}
		catch (Win32Exception)
		{
			return false;
		}
	}

	private async Task SetInstalledAsync(string packageId, bool installed, CancellationToken cancellationToken)
	{
		string[] arguments = ((!installed) ? new string[7] { "uninstall", "--id", packageId, "-e", "--silent", "--accept-source-agreements", "--disable-interactivity" } : new string[11]
		{
			"install", "--id", packageId, "-e", "--source", "winget", "--silent", "--no-upgrade", "--accept-package-agreements", "--accept-source-agreements",
			"--disable-interactivity"
		});
		ProcessResult processResult;
		try
		{
			processResult = await _process.RunAsync(ResolveWinget(), arguments, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Win32Exception innerException)
		{
			throw new InvalidOperationException("WinGet is not available. Install or update Microsoft App Installer, then try again.", innerException);
		}
		if (!processResult.Succeeded)
		{
			throw new InvalidOperationException($"WinGet could not {(installed ? "install" : "remove")} {packageId}: {processResult.CombinedOutput}");
		}
	}

	private static void ValidatePackageId(string packageId)
	{
		if (!PackageIdRegex.IsMatch(packageId))
		{
			throw new ManifestValidationException("Invalid WinGet package ID '" + packageId + "'.");
		}
	}

	private static string ResolveWinget()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
		if (!File.Exists(text))
		{
			return "winget.exe";
		}
		return text;
	}
}

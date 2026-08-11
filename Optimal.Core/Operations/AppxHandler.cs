using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class AppxHandler : IOperationHandler, IConditionHandler, IBackupRestorer
{
	private readonly IProcessRunner _process;

	public string Type => "appx";

	private static Regex PackageNameRegex { get; } = new("^[A-Za-z0-9][A-Za-z0-9._-]{1,127}$", RegexOptions.CultureInvariant);

	public AppxHandler(IProcessRunner process)
	{
		_process = process;
	}

	public void Validate(OperationSpec spec)
	{
		ValidateName(spec.RequireString("packageName"));
		string text = spec.RequireString("action");
		if (!text.Equals("remove", StringComparison.OrdinalIgnoreCase) && !text.Equals("restore", StringComparison.OrdinalIgnoreCase))
		{
			throw new ManifestValidationException("appx action must be remove or restore.");
		}
	}

	public void Validate(ConditionSpec spec)
	{
		ValidateName(spec.RequireString("packageName"));
		spec.Require("installed").GetBoolean();
	}

	public string Describe(OperationSpec spec)
	{
		return spec.RequireString("action") + " current-user AppX package " + spec.RequireString("packageName");
	}

	public string Describe(ConditionSpec spec)
	{
		return "AppX package " + spec.RequireString("packageName") + " is " + (spec.Require("installed").GetBoolean() ? "installed" : "not installed");
	}

	public async Task<bool> EvaluateAsync(ConditionSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		return await FindManifestAsync(spec.RequireString("packageName"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false) != null == spec.Require("installed").GetBoolean();
	}

	public async Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		string name = spec.RequireString("packageName");
		string text = await FindManifestAsync(name, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new AppxPackageBackup
		{
			PackageName = name,
			WasInstalled = (text != null),
			ManifestPath = text,
			Describe = name + " was " + ((text == null) ? "not installed" : "registered for the current user")
		});
	}

	public async Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping: {Description}", Describe(spec));
			return;
		}
		string name = spec.RequireString("packageName");
		if (spec.RequireString("action").Equals("remove", StringComparison.OrdinalIgnoreCase))
		{
			await RemoveAsync(name, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		string text = await FindManifestFromAnyUserAsync(name, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (text == null)
		{
			throw new InvalidOperationException("Windows no longer has installation files for " + name + "; reinstall it from Microsoft Store.");
		}
		await RegisterAsync(text, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public bool CanRestore(BackupEntry entry)
	{
		return entry is AppxPackageBackup;
	}

	public async Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken)
	{
		AppxPackageBackup backup = (AppxPackageBackup)entry;
		bool flag = await FindManifestAsync(backup.PackageName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) != null;
		if (backup.WasInstalled && !flag)
		{
			string text = backup.ManifestPath;
			if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
			{
				text = await FindManifestFromAnyUserAsync(backup.PackageName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (text == null)
			{
				throw new InvalidOperationException("Could not restore " + backup.PackageName + "; its AppX manifest is no longer present.");
			}
			await RegisterAsync(text, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (!backup.WasInstalled && flag)
		{
			await RemoveAsync(backup.PackageName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task<string?> FindManifestAsync(string name, CancellationToken cancellationToken)
	{
		string script = "$p=Get-AppxPackage -Name '" + name + "' | Select-Object -First 1; if($p){Join-Path $p.InstallLocation 'AppxManifest.xml'}";
		string text = (await RunPowerShellAsync(script, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).StandardOutput.Trim();
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}

	private async Task<string?> FindManifestFromAnyUserAsync(string name, CancellationToken cancellationToken)
	{
		string script = "$p=Get-AppxPackage -AllUsers -Name '" + name + "' | Select-Object -First 1; if($p){Join-Path $p.InstallLocation 'AppxManifest.xml'}";
		string text = (await RunPowerShellAsync(script, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).StandardOutput.Trim();
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}

	private async Task RemoveAsync(string name, CancellationToken cancellationToken)
	{
		ProcessResult processResult = await RunPowerShellAsync("Get-AppxPackage -Name '" + name + "' | Remove-AppxPackage -ErrorAction Stop", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!processResult.Succeeded)
		{
			throw new InvalidOperationException("Could not remove " + name + ": " + processResult.CombinedOutput);
		}
	}

	private async Task RegisterAsync(string manifestPath, CancellationToken cancellationToken)
	{
		string text = manifestPath.Replace("'", "''", StringComparison.Ordinal);
		ProcessResult processResult = await RunPowerShellAsync("Add-AppxPackage -DisableDevelopmentMode -Register '" + text + "' -ErrorAction Stop", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!processResult.Succeeded)
		{
			throw new InvalidOperationException("Could not restore AppX package: " + processResult.CombinedOutput);
		}
	}

	private Task<ProcessResult> RunPowerShellAsync(string script, CancellationToken cancellationToken)
	{
		return _process.RunAsync("powershell.exe", new _003C_003Ez__ReadOnlyArray<string>(new string[6] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }), cancellationToken);
	}

	private static void ValidateName(string name)
	{
		if (!PackageNameRegex.IsMatch(name))
		{
			throw new ManifestValidationException("Invalid AppX package name '" + name + "'.");
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class NvidiaProfileHandler : IOperationHandler, IBackupRestorer
{
	private readonly IProcessRunner _process;

	public string Type => "nvidiaProfile";

	public NvidiaProfileHandler(IProcessRunner process)
	{
		_process = process;
	}

	public void Validate(OperationSpec spec)
	{
		string text = spec.RequireString("profile");
		if (Path.GetFileName(text) != text || !text.EndsWith(".nip", StringComparison.OrdinalIgnoreCase))
		{
			throw new ManifestValidationException("nvidiaProfile 'profile' must be a .nip filename without a path.");
		}
	}

	public string Describe(OperationSpec spec)
	{
		return "Back up NVIDIA custom profiles and import " + spec.RequireString("profile");
	}

	public async Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		string inspector = (await FindInspectorAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? throw new InvalidOperationException("NVIDIA Profile Inspector is not installed. Select its installer first, complete that run, then apply this profile.");
		string directory = Path.GetDirectoryName(inspector);
		HashSet<string> before = Directory.GetFiles(directory, "*.nip").ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!context.DryRun)
		{
			ProcessResult processResult = await _process.RunAsync(inspector, new _003C_003Ez__ReadOnlySingleElementList<string>("-exportCustomized"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!processResult.Succeeded)
			{
				throw new InvalidOperationException("NVIDIA profile backup failed: " + processResult.CombinedOutput);
			}
		}
		string text = (from path in Directory.GetFiles(directory, "*.nip")
			where !before.Contains(path)
			select path).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
		return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new NvidiaProfileBackup
		{
			InspectorPath = inspector,
			BackupPath = text,
			Describe = ((text == null) ? "No customized NVIDIA profiles existed" : ("Exported NVIDIA profiles to " + text))
		});
	}

	public async Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		if (!context.DryRun)
		{
			string fileName = (await FindInspectorAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? throw new InvalidOperationException("NVIDIA Profile Inspector is not installed.");
			string text = Path.Combine(AppContext.BaseDirectory, "profiles", "nvidia", spec.RequireString("profile"));
			if (!File.Exists(text))
			{
				throw new FileNotFoundException("The selected NVIDIA profile is missing from this Optimal installation.", text);
			}
			ProcessResult processResult = await _process.RunAsync(fileName, new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "-silentImport", text }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!processResult.Succeeded)
			{
				throw new InvalidOperationException("NVIDIA profile import failed: " + processResult.CombinedOutput);
			}
		}
	}

	public bool CanRestore(BackupEntry entry)
	{
		return entry is NvidiaProfileBackup;
	}

	public async Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken)
	{
		NvidiaProfileBackup nvidiaProfileBackup = (NvidiaProfileBackup)entry;
		if (!string.IsNullOrWhiteSpace(nvidiaProfileBackup.BackupPath) && File.Exists(nvidiaProfileBackup.BackupPath))
		{
			ProcessResult processResult = await _process.RunAsync(nvidiaProfileBackup.InspectorPath, new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "-silentImport", nvidiaProfileBackup.BackupPath }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!processResult.Succeeded)
			{
				throw new InvalidOperationException("NVIDIA profile restore failed: " + processResult.CombinedOutput);
			}
		}
	}

	private async Task<string?> FindInspectorAsync(CancellationToken cancellationToken)
	{
		string[] candidates = new string[3]
		{
			Path.Combine(AppContext.BaseDirectory, "tools", "nvidiaProfileInspector.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "nvidiaProfileInspector.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Profile Inspector", "nvidiaProfileInspector.exe")
		};
		string text = candidates.FirstOrDefault(File.Exists);
		if (text != null)
		{
			return text;
		}
		try
		{
			ProcessResult processResult = await _process.RunAsync("where.exe", new _003C_003Ez__ReadOnlySingleElementList<string>("nvidiaProfileInspector.exe"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return processResult.Succeeded ? processResult.StandardOutput.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : null;
		}
		catch
		{
			return null;
		}
	}
}

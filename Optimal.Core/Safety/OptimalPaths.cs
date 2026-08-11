using System;
using System.IO;

namespace Optimal.Core.Safety;

public sealed class OptimalPaths
{
	public string Root { get; }

	public string Logs { get; }

	public string Backups { get; }

	public string Journal { get; }

	public string Profiles { get; }

	public OptimalPaths(string? rootOverride = null)
	{
		Root = rootOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimal");
		Logs = Path.Combine(Root, "logs");
		Backups = Path.Combine(Root, "backups");
		Journal = Path.Combine(Root, "journal");
		Profiles = Path.Combine(Root, "profiles");
	}

	public void EnsureCreated()
	{
		Directory.CreateDirectory(Root);
		Directory.CreateDirectory(Logs);
		Directory.CreateDirectory(Backups);
		Directory.CreateDirectory(Journal);
		Directory.CreateDirectory(Profiles);
	}

	public string BackupDirectoryForRun(string runId)
	{
		return Path.Combine(Backups, runId);
	}

	public string JournalFileForRun(string runId)
	{
		bool flag = string.IsNullOrWhiteSpace(runId) || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || runId.Contains(Path.DirectorySeparatorChar) || runId.Contains(Path.AltDirectorySeparatorChar);
		if (!flag)
		{
			bool flag2 = ((runId == "." || runId == "..") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			throw new ArgumentException("Run id is not a valid journal identifier.", "runId");
		}
		return Path.Combine(Journal, runId + ".json");
	}
}

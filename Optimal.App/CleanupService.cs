using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Optimal.App;

internal sealed class CleanupService
{
	public IReadOnlyList<CleanupTarget> Targets { get; } = new global::_003C_003Ez__ReadOnlyArray<CleanupTarget>(new CleanupTarget[4]
	{
		new CleanupTarget("User temporary files", Path.GetTempPath(), TimeSpan.FromHours(24)),
		new CleanupTarget("Windows temporary files", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), TimeSpan.FromHours(24)),
		new CleanupTarget("DirectX shader cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), TimeSpan.FromHours(24)),
		new CleanupTarget("Application crash dumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"), TimeSpan.FromDays(1))
	});

	public Task<CleanupSummary> AnalyzeAsync(IEnumerable<CleanupTarget> targets, CancellationToken cancellationToken)
	{
		return Task.Run(() => EnumerateCandidates(targets, cancellationToken).Aggregate(new CleanupSummary(0L, 0), (CleanupSummary summary, FileInfo file) => new CleanupSummary(summary.Bytes + file.Length, summary.Files + 1)), cancellationToken);
	}

	public Task<CleanupSummary> CleanAsync(IEnumerable<CleanupTarget> targets, CancellationToken cancellationToken)
	{
		return Task.Run(delegate
		{
			long num = 0L;
			int num2 = 0;
			foreach (FileInfo item in EnumerateCandidates(targets, cancellationToken))
			{
				try
				{
					long length = item.Length;
					item.Delete();
					num += length;
					num2++;
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
				}
			}
			return new CleanupSummary(num, num2);
		}, cancellationToken);
	}

	private static IEnumerable<FileInfo> EnumerateCandidates(IEnumerable<CleanupTarget> targets, CancellationToken cancellationToken)
	{
		foreach (CleanupTarget target in targets)
		{
			if (!Directory.Exists(target.Path))
			{
				continue;
			}
			string root = Path.GetFullPath(target.Path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
			stack.Push(new DirectoryInfo(target.Path));
			while (stack.Count > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				DirectoryInfo directoryInfo = stack.Pop();
				if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != FileAttributes.None)
				{
					continue;
				}
				FileSystemInfo[] fileSystemInfos;
				try
				{
					fileSystemInfos = directoryInfo.GetFileSystemInfos();
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
					continue;
				}
				FileSystemInfo[] array = fileSystemInfos;
				foreach (FileSystemInfo fileSystemInfo in array)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!Path.GetFullPath(fileSystemInfo.FullName).StartsWith(root, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					if (fileSystemInfo is DirectoryInfo directoryInfo2)
					{
						if ((directoryInfo2.Attributes & FileAttributes.ReparsePoint) == 0)
						{
							stack.Push(directoryInfo2);
						}
					}
					else if (fileSystemInfo is FileInfo fileInfo && DateTime.UtcNow - fileInfo.LastWriteTimeUtc >= target.MinimumAge)
					{
						yield return fileInfo;
					}
				}
			}
		}
	}
}

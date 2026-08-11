using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;

namespace Optimal.Core.Safety;

public sealed class RegistryBackupService
{
	private readonly IProcessRunner _process;

	private readonly ILogger<RegistryBackupService> _logger;

	public RegistryBackupService(IProcessRunner process, ILogger<RegistryBackupService> logger)
	{
		_process = process;
		_logger = logger;
	}

	public async Task<IReadOnlyList<string>> ExportAsync(IEnumerable<OperationSpec> operations, string destinationDirectory, CancellationToken cancellationToken)
	{
		List<RegistryPath> list = CollectRegistryKeys(operations);
		if (list.Count == 0)
		{
			return Array.Empty<string>();
		}
		Directory.CreateDirectory(destinationDirectory);
		List<string> written = new List<string>(list.Count);
		foreach (RegistryPath key in list)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string fileName = MakeFileName(key) + ".reg";
			string destination = Path.Combine(destinationDirectory, fileName);
			List<string> list2 = new List<string> { "export", key.FullPath, destination, "/y" };
			if (key.ViewBits == 32)
			{
				list2.Add("/reg:32");
			}
			ProcessResult processResult = await _process.RunAsync("reg.exe", list2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (processResult.Succeeded)
			{
				written.Add(destination);
				_logger.LogDebug("Exported {Key} to {File}", key.FullPath, fileName);
			}
			else
			{
				_logger.LogDebug("Skipped export of {Key}: {Output}", key.FullPath, processResult.CombinedOutput);
			}
		}
		_logger.LogInformation("Exported {Count} registry keys to {Directory}", written.Count, destinationDirectory);
		return written;
	}

	private static List<RegistryPath> CollectRegistryKeys(IEnumerable<OperationSpec> operations)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<RegistryPath> list = new List<RegistryPath>();
		foreach (OperationSpec operation in operations)
		{
			if (!string.Equals(operation.Type, "registry", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string text = operation.OptionalString("path");
			if (text != null)
			{
				RegistryPath registryPath;
				try
				{
					registryPath = RegistryPath.Parse(text, operation.OptionalInt("view", 64));
				}
				catch (ManifestValidationException)
				{
					continue;
				}
				if (hashSet.Add($"{registryPath.FullPath}|{registryPath.ViewBits}"))
				{
					list.Add(registryPath);
				}
			}
		}
		return list;
	}

	private static string MakeFileName(RegistryPath path)
	{
		string source = path.FullPath.Replace('\\', '_');
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(source.Select((char c) => (!invalid.Contains(c)) ? c : '_').ToArray());
		if (text.Length > 120)
		{
			int num = Math.Abs(path.FullPath.GetHashCode(StringComparison.OrdinalIgnoreCase));
			text = string.Concat(text.AsSpan(0, 120), "_", num.ToString());
		}
		if (path.ViewBits != 32)
		{
			return text;
		}
		return text + "_wow32";
	}
}

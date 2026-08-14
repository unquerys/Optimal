using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Optimal.Core.Operations;

namespace Optimal.App;

internal sealed class DebloatScanService
{
	private readonly IProcessRunner _process;

	public DebloatScanService(IProcessRunner process) => _process = process;

	public async Task<IReadOnlySet<string>> ScanCurrentUserPackagesAsync(CancellationToken cancellationToken)
	{
		ProcessResult result = await _process.RunAsync(
			"powershell.exe",
			new[]
			{
				"-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
				"Get-AppxPackage | ForEach-Object { $_.Name }"
			},
			cancellationToken).ConfigureAwait(false);

		if (!result.Succeeded)
			throw new InvalidOperationException("Windows package inventory failed: " + result.CombinedOutput.Trim());

		return result.StandardOutput
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}
}

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Optimal.Core.Operations;

public sealed class PowerCfg
{
	private readonly IProcessRunner _process;

	public const string UltimatePerformanceTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

	private static Regex SchemeLineRegex { get; } = new("Power Scheme GUID:\\s*(?<guid>[0-9a-fA-F-]{36})(?:\\s*\\((?<name>[^)]*)\\))?", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

	private static Regex SettingIndexRegex { get; } = new("Current (?<rail>AC|DC) Power Setting Index:\\s*(?<value>0x[0-9a-fA-F]+|\\d+)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

	public PowerCfg(IProcessRunner process)
	{
		_process = process;
	}

	public async Task<(string Guid, string? Name)> GetActiveSchemeAsync(CancellationToken cancellationToken)
	{
		ProcessResult processResult = await RunAsync(new string[1] { "/getactivescheme" }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Match match = SchemeLineRegex.Match(processResult.StandardOutput);
		if (!match.Success)
		{
			throw new InvalidOperationException("Could not read the active power scheme from powercfg output: " + processResult.CombinedOutput);
		}
		string item = (match.Groups["name"].Success ? match.Groups["name"].Value.Trim() : null);
		return (Guid: match.Groups["guid"].Value, Name: item);
	}

	public async Task SetActiveSchemeAsync(string schemeGuid, CancellationToken cancellationToken)
	{
		await RunAsync(new string[2] { "/setactive", schemeGuid }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<bool> SchemeExistsAsync(string schemeGuid, CancellationToken cancellationToken)
	{
		return (await RunAsync(new string[1] { "/list" }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).StandardOutput.Contains(schemeGuid, StringComparison.OrdinalIgnoreCase);
	}

	public async Task<string> DuplicateSchemeAsync(string sourceGuid, string? destinationGuid, CancellationToken cancellationToken)
	{
		string[] arguments = ((destinationGuid != null) ? new string[3] { "/duplicatescheme", sourceGuid, destinationGuid } : new string[2] { "/duplicatescheme", sourceGuid });
		ProcessResult processResult = await RunAsync(arguments, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Match match = SchemeLineRegex.Match(processResult.StandardOutput);
		if (!match.Success)
		{
			throw new InvalidOperationException("powercfg did not report a GUID for the duplicated scheme: " + processResult.CombinedOutput);
		}
		return match.Groups["guid"].Value;
	}

	public async Task RenameSchemeAsync(string schemeGuid, string name, string? description, CancellationToken cancellationToken)
	{
		string[] arguments = ((description != null) ? new string[4] { "/changename", schemeGuid, name, description } : new string[3] { "/changename", schemeGuid, name });
		await RunAsync(arguments, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task DeleteSchemeAsync(string schemeGuid, CancellationToken cancellationToken)
	{
		await RunAsync(new string[2] { "/delete", schemeGuid }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<(uint? Ac, uint? Dc)> QuerySettingAsync(string schemeGuid, string subgroupGuid, string settingGuid, CancellationToken cancellationToken)
	{
		ProcessResult processResult = await _process.RunAsync("powercfg.exe", new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "/query", schemeGuid, subgroupGuid, settingGuid }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!processResult.Succeeded)
		{
			return (Ac: null, Dc: null);
		}
		uint? item = null;
		uint? item2 = null;
		foreach (Match item3 in SettingIndexRegex.Matches(processResult.StandardOutput))
		{
			uint value = ParseIndex(item3.Groups["value"].Value);
			if (item3.Groups["rail"].Value == "AC")
			{
				item = value;
			}
			else
			{
				item2 = value;
			}
		}
		return (Ac: item, Dc: item2);
	}

	public async Task SetSettingAsync(string schemeGuid, string subgroupGuid, string settingGuid, uint? acValue, uint? dcValue, CancellationToken cancellationToken)
	{
		if (acValue.HasValue)
		{
			uint valueOrDefault = acValue.GetValueOrDefault();
			await RunAsync(new string[5]
			{
				"/setacvalueindex",
				schemeGuid,
				subgroupGuid,
				settingGuid,
				valueOrDefault.ToString(CultureInfo.InvariantCulture)
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (dcValue.HasValue)
		{
			uint valueOrDefault2 = dcValue.GetValueOrDefault();
			await RunAsync(new string[5]
			{
				"/setdcvalueindex",
				schemeGuid,
				subgroupGuid,
				settingGuid,
				valueOrDefault2.ToString(CultureInfo.InvariantCulture)
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task UnhideSettingAsync(string subgroupGuid, string settingGuid, CancellationToken cancellationToken)
	{
		await RunAsync(new string[4] { "/attributes", subgroupGuid, settingGuid, "-ATTRIB_HIDE" }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<ProcessResult> RunAsync(string[] arguments, CancellationToken cancellationToken)
	{
		ProcessResult processResult = await _process.RunAsync("powercfg.exe", arguments, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!processResult.Succeeded)
		{
			throw new InvalidOperationException($"powercfg {string.Join(' ', arguments)} failed with exit code {processResult.ExitCode}: {processResult.CombinedOutput}");
		}
		return processResult;
	}

	private static uint ParseIndex(string text)
	{
		if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return uint.Parse(text, CultureInfo.InvariantCulture);
		}
		return uint.Parse(text.Substring(2, text.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
	}
}

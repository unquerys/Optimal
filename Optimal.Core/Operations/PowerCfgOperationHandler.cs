using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class PowerCfgOperationHandler : IOperationHandler, IBackupRestorer
{
	private const string ActionSetActiveScheme = "setactivescheme";

	private const string ActionSetSetting = "setsetting";

	private const string ActionDuplicateScheme = "duplicatescheme";

	private const string ActionUnhideSetting = "unhidesetting";

	private static readonly Dictionary<string, string> WellKnownSchemes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["balanced"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
		["highperformance"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
		["powersaver"] = "a1841308-3541-4fab-bc81-f71556f20b4a",
		["ultimate"] = "e9a42b02-d5df-448d-aa00-03f14749eb61"
	};

	private readonly PowerCfg _powerCfg;

	public string Type => "powercfg";

	public PowerCfgOperationHandler(PowerCfg powerCfg)
	{
		_powerCfg = powerCfg;
	}

	public void Validate(OperationSpec spec)
	{
		string text = ReadAction(spec);
		switch (text)
		{
		case "setactivescheme":
			ResolveScheme(spec.RequireString("scheme"));
			break;
		case "setsetting":
			ResolveScheme(spec.RequireString("scheme"));
			RequireGuid(spec, "subgroup");
			RequireGuid(spec, "setting");
			if (!spec.Optional("ac").HasValue && !spec.Optional("dc").HasValue)
			{
				throw new ManifestValidationException("powercfg setSetting needs at least one of 'ac' or 'dc'.");
			}
			break;
		case "duplicatescheme":
		{
			ResolveScheme(spec.RequireString("source"));
			string text2 = spec.OptionalString("destination");
			if (text2 != null && !Guid.TryParse(text2, out var _))
			{
				throw new ManifestValidationException("powercfg duplicateScheme 'destination' must be a GUID, found '" + text2 + "'.");
			}
			break;
		}
		case "unhidesetting":
			RequireGuid(spec, "subgroup");
			RequireGuid(spec, "setting");
			break;
		default:
			throw new ManifestValidationException("Unrecognised powercfg action '" + text + "'. Expected setActiveScheme, setSetting, duplicateScheme, or unhideSetting.");
		}
	}

	public string Describe(OperationSpec spec)
	{
		string text = ReadAction(spec);
		return text switch
		{
			"setactivescheme" => $"Activate power scheme {spec.RequireString("scheme")} ({ResolveScheme(spec.RequireString("scheme"))})", 
			"setsetting" => $"Set power setting {spec.RequireString("setting")} in scheme {spec.RequireString("scheme")} to AC={DescribeRail(spec, "ac")}, DC={DescribeRail(spec, "dc")}", 
			"duplicatescheme" => "Create power scheme '" + (spec.OptionalString("name") ?? "copy") + "' from " + spec.RequireString("source"), 
			"unhidesetting" => "Make power setting " + spec.RequireString("setting") + " visible in Windows power options", 
			_ => "powercfg " + text, 
		};
	}

	public async Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		switch (ReadAction(spec))
		{
		case "setactivescheme":
			var (text, text2) = await _powerCfg.GetActiveSchemeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new PowerSchemeBackup
			{
				ActiveGuid = text,
				FriendlyName = text2,
				Describe = "Active power scheme was " + (text2 ?? text)
			});
		case "setsetting":
		{
			string scheme = ResolveScheme(spec.RequireString("scheme"));
			string subgroup = spec.RequireString("subgroup");
			string setting = spec.RequireString("setting");
			var (num, num2) = await _powerCfg.QuerySettingAsync(scheme, subgroup, setting, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new PowerSettingBackup
			{
				SchemeGuid = scheme,
				SubgroupGuid = subgroup,
				SettingGuid = setting,
				AcValue = num,
				DcValue = num2,
				Describe = $"Power setting {setting} was AC={Show(num)}, DC={Show(num2)}"
			});
		}
		case "duplicatescheme":
		{
			string setting = spec.OptionalString("destination");
			bool flag = setting == null;
			if (!flag)
			{
				flag = await _powerCfg.SchemeExistsAsync(setting, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (flag)
			{
				return Array.Empty<BackupEntry>();
			}
			return new _003C_003Ez__ReadOnlySingleElementList<BackupEntry>(new PowerSchemeCreatedBackup
			{
				SchemeGuid = setting,
				Describe = "Power scheme " + setting + " did not exist"
			});
		}
		default:
			return Array.Empty<BackupEntry>();
		}
	}

	public async Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping: {Description}", Describe(spec));
			return;
		}
		switch (ReadAction(spec))
		{
		case "setactivescheme":
		{
			string scheme = ResolveScheme(spec.RequireString("scheme"));
			if (!(await _powerCfg.SchemeExistsAsync(scheme, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
			{
				context.Logger.LogInformation("Scheme {Scheme} is not present, importing it first.", scheme);
				scheme = await _powerCfg.DuplicateSchemeAsync(scheme, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await _powerCfg.SetActiveSchemeAsync(scheme, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			break;
		}
		case "setsetting":
		{
			string schemeGuid = ResolveScheme(spec.RequireString("scheme"));
			await _powerCfg.SetSettingAsync(schemeGuid, spec.RequireString("subgroup"), spec.RequireString("setting"), ReadRail(spec, "ac"), ReadRail(spec, "dc"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			break;
		}
		case "duplicatescheme":
		{
			string scheme = ResolveScheme(spec.RequireString("source"));
			string destination = spec.OptionalString("destination");
			bool flag = destination != null;
			if (flag)
			{
				flag = await _powerCfg.SchemeExistsAsync(destination, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (flag)
			{
				context.Logger.LogInformation("Scheme {Scheme} already exists, leaving it alone.", destination);
			}
			else
			{
				destination = await _powerCfg.DuplicateSchemeAsync(scheme, destination, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			string text = spec.OptionalString("name");
			if (text != null)
			{
				await _powerCfg.RenameSchemeAsync(destination, text, spec.OptionalString("description"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			break;
		}
		case "unhidesetting":
			await _powerCfg.UnhideSettingAsync(spec.RequireString("subgroup"), spec.RequireString("setting"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			break;
		}
	}

	public bool CanRestore(BackupEntry entry)
	{
		if (entry is PowerSchemeBackup || entry is PowerSettingBackup || entry is PowerSchemeCreatedBackup)
		{
			return true;
		}
		return false;
	}

	public async Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken)
	{
		if (context.DryRun)
		{
			context.Logger.LogInformation("Dry run, skipping restore: {Description}", entry.Describe);
		}
		else if (!(entry is PowerSchemeBackup powerSchemeBackup))
		{
			if (!(entry is PowerSettingBackup powerSettingBackup))
			{
				if (entry is PowerSchemeCreatedBackup created && await _powerCfg.SchemeExistsAsync(created.SchemeGuid, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
				{
					await _powerCfg.DeleteSchemeAsync(created.SchemeGuid, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			else
			{
				await _powerCfg.SetSettingAsync(powerSettingBackup.SchemeGuid, powerSettingBackup.SubgroupGuid, powerSettingBackup.SettingGuid, powerSettingBackup.AcValue, powerSettingBackup.DcValue, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		else
		{
			await _powerCfg.SetActiveSchemeAsync(powerSchemeBackup.ActiveGuid, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static string ResolveScheme(string token)
	{
		if (WellKnownSchemes.TryGetValue(token, out string value))
		{
			return value;
		}
		if (Guid.TryParse(token, out var result))
		{
			return result.ToString();
		}
		string value2 = string.Join(", ", WellKnownSchemes.Keys);
		throw new ManifestValidationException($"Power scheme '{token}' is neither a GUID nor a known token. Known tokens: {value2}.");
	}

	private static void RequireGuid(OperationSpec spec, string propertyName)
	{
		string text = spec.RequireString(propertyName);
		if (!Guid.TryParse(text, out var _))
		{
			throw new ManifestValidationException($"powercfg operation expects '{propertyName}' to be a GUID, found '{text}'.");
		}
	}

	private static uint? ReadRail(OperationSpec spec, string propertyName)
	{
		JsonElement? jsonElement = spec.Optional(propertyName);
		if (!jsonElement.HasValue)
		{
			return null;
		}
		if (jsonElement.Value.ValueKind != JsonValueKind.Number || !jsonElement.Value.TryGetUInt32(out var value))
		{
			throw new ManifestValidationException("powercfg setSetting expects '" + propertyName + "' to be a non-negative integer.");
		}
		return value;
	}

	private static string DescribeRail(OperationSpec spec, string propertyName)
	{
		return Show(ReadRail(spec, propertyName));
	}

	private static string Show(uint? value)
	{
		return value?.ToString() ?? "unchanged";
	}

	private static string ReadAction(OperationSpec spec)
	{
		return spec.RequireString("action").ToLowerInvariant();
	}
}

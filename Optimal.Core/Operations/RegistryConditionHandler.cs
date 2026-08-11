using System;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class RegistryConditionHandler : IConditionHandler
{
	public string Type => "registry";

	public void Validate(ConditionSpec spec)
	{
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), spec.OptionalInt("view", 64));
		if (string.IsNullOrEmpty(registryPath.SubKey))
		{
			throw new ManifestValidationException("Registry condition targets the root of " + registryPath.Hive + ", which is never correct.");
		}
		string[] array = new string[3] { "equals", "notEquals", "exists" }.Where((string name) => spec.Optional(name).HasValue).ToArray();
		if (array.Length != 1)
		{
			throw new ManifestValidationException((array.Length == 0) ? "Registry condition needs exactly one of 'equals', 'notEquals', or 'exists'." : ("Registry condition has more than one comparison (" + string.Join(", ", array) + "). Author exactly one."));
		}
		JsonElement? jsonElement = spec.Optional("exists");
		JsonElement valueOrDefault = default(JsonElement);
		int num;
		if (jsonElement.HasValue)
		{
			valueOrDefault = jsonElement.GetValueOrDefault();
			num = 1;
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		if (flag)
		{
			JsonValueKind valueKind = valueOrDefault.ValueKind;
			bool flag2 = valueKind - 5 <= JsonValueKind.Object;
			flag = !flag2;
		}
		if (flag)
		{
			throw new ManifestValidationException("Registry condition 'exists' must be true or false.");
		}
	}

	public string Describe(ConditionSpec spec)
	{
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), spec.OptionalInt("view", 64));
		string text = spec.OptionalString("name") ?? "(default)";
		JsonElement? jsonElement = spec.Optional("exists");
		if (jsonElement.HasValue)
		{
			if (jsonElement.GetValueOrDefault().ValueKind != JsonValueKind.True)
			{
				return registryPath.FullPath + "\\" + text + " does not exist";
			}
			return registryPath.FullPath + "\\" + text + " exists";
		}
		jsonElement = spec.Optional("notEquals");
		if (jsonElement.HasValue)
		{
			JsonElement valueOrDefault = jsonElement.GetValueOrDefault();
			return $"{registryPath.FullPath}\\{text} is not {valueOrDefault.GetRawText()}";
		}
		return $"{registryPath.FullPath}\\{text} is {spec.Require("equals").GetRawText()}";
	}

	public Task<bool> EvaluateAsync(ConditionSpec spec, OperationContext context, CancellationToken cancellationToken)
	{
		RegistryPath registryPath = RegistryPath.Parse(spec.RequireString("path"), spec.OptionalInt("view", 64));
		string text = spec.OptionalString("name") ?? string.Empty;
		object obj;
		try
		{
			using RegistryKey registryKey = registryPath.OpenBaseKey();
			using RegistryKey registryKey2 = registryKey.OpenSubKey(registryPath.SubKey, writable: false);
			obj = registryKey2?.GetValue(text, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
		}
		catch (Exception ex) when (((ex is SecurityException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			context.Logger.LogDebug(ex, "Could not read {Path}\\{Name} while detecting.", registryPath.FullPath, text);
			return Task.FromResult(result: false);
		}
		JsonElement? jsonElement = spec.Optional("exists");
		if (jsonElement.HasValue)
		{
			bool flag = jsonElement.GetValueOrDefault().ValueKind == JsonValueKind.True;
			return Task.FromResult(obj != null == flag);
		}
		jsonElement = spec.Optional("notEquals");
		if (jsonElement.HasValue)
		{
			JsonElement valueOrDefault = jsonElement.GetValueOrDefault();
			return Task.FromResult(obj != null && !ValuesMatch(obj, valueOrDefault));
		}
		return Task.FromResult(obj != null && ValuesMatch(obj, spec.Require("equals")));
	}

	private static bool ValuesMatch(object actual, JsonElement expected)
	{
		if (!(actual is int) && !(actual is long))
		{
			if (!(actual is string b))
			{
				if (!(actual is string[] second))
				{
					if (actual is byte[] inArray)
					{
						if (expected.ValueKind == JsonValueKind.String)
						{
							return Convert.ToHexString(inArray).Equals(expected.GetString()?.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
						}
						return false;
					}
					return false;
				}
				if (expected.ValueKind == JsonValueKind.Array)
				{
					return (from e in expected.EnumerateArray()
						select e.GetString()).SequenceEqual<string>(second);
				}
				return false;
			}
			if (expected.ValueKind == JsonValueKind.String)
			{
				return string.Equals(expected.GetString(), b, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		long num = Convert.ToInt64(actual, CultureInfo.InvariantCulture);
		long value;
		long result;
		return expected.ValueKind switch
		{
			JsonValueKind.Number => expected.TryGetInt64(out value) && value == num, 
			JsonValueKind.True => num == 1, 
			JsonValueKind.False => num == 0, 
			JsonValueKind.String => long.TryParse(expected.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result == num, 
			_ => false, 
		};
	}
}

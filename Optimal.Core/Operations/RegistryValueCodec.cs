using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public static class RegistryValueCodec
{
	public static RegistryValueKind ParseKind(string token)
	{
		switch (token.ToLowerInvariant())
		{
		case "dword":
		case "reg_dword":
			return RegistryValueKind.DWord;
		case "qword":
		case "reg_qword":
			return RegistryValueKind.QWord;
		case "reg_sz":
		case "string":
		case "sz":
			return RegistryValueKind.String;
		case "expand_sz":
		case "expandstring":
		case "reg_expand_sz":
			return RegistryValueKind.ExpandString;
		case "reg_multi_sz":
		case "multistring":
		case "multi_sz":
			return RegistryValueKind.MultiString;
		case "binary":
		case "reg_binary":
			return RegistryValueKind.Binary;
		default:
			throw new ManifestValidationException("Unrecognised registry valueType '" + token + "'. Expected dword, qword, string, expandString, multiString, or binary.");
		}
	}

	public static string KindToToken(RegistryValueKind kind)
	{
		return kind switch
		{
			RegistryValueKind.DWord => "dword", 
			RegistryValueKind.QWord => "qword", 
			RegistryValueKind.String => "string", 
			RegistryValueKind.ExpandString => "expandString", 
			RegistryValueKind.MultiString => "multiString", 
			RegistryValueKind.Binary => "binary", 
			_ => "unknown", 
		};
	}

	public static object FromJson(JsonElement element, RegistryValueKind kind)
	{
		try
		{
			switch (kind)
			{
			case RegistryValueKind.DWord:
				return ReadDword(element);
			case RegistryValueKind.QWord:
				return ReadQword(element);
			case RegistryValueKind.String:
			case RegistryValueKind.ExpandString:
				return ReadString(element);
			case RegistryValueKind.MultiString:
				return ReadMultiString(element);
			case RegistryValueKind.Binary:
				return ReadBinary(element);
			default:
				throw new ManifestValidationException($"Cannot write registry value of kind {kind}.");
			}
		}
		catch (Exception ex) when (((ex is FormatException || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			throw new ManifestValidationException("Registry value could not be read as " + KindToToken(kind) + ": " + ex.Message, ex);
		}
	}

	public static string? ToStorage(object? value, RegistryValueKind kind)
	{
		if (value != null)
		{
			if (!(value is byte[] inArray))
			{
				if (!(value is string[] value2))
				{
					if (!(value is int num))
					{
						if (value is long num2)
						{
							return num2.ToString(CultureInfo.InvariantCulture);
						}
						return Convert.ToString(value, CultureInfo.InvariantCulture);
					}
					return num.ToString(CultureInfo.InvariantCulture);
				}
				return JsonSerializer.Serialize(value2);
			}
			return Convert.ToBase64String(inArray);
		}
		return null;
	}

	public static object FromStorage(string? data, RegistryValueKind kind)
	{
		string text = data ?? string.Empty;
		switch (kind)
		{
		case RegistryValueKind.DWord:
			return (int)long.Parse(text, CultureInfo.InvariantCulture);
		case RegistryValueKind.QWord:
			return long.Parse(text, CultureInfo.InvariantCulture);
		case RegistryValueKind.String:
		case RegistryValueKind.ExpandString:
			return text;
		case RegistryValueKind.MultiString:
			return JsonSerializer.Deserialize<string[]>(text) ?? Array.Empty<string>();
		case RegistryValueKind.Binary:
			return Convert.FromBase64String(text);
		default:
			throw new InvalidOperationException($"Cannot restore a registry value of kind {kind} from the journal.");
		}
	}

	private static int ReadDword(JsonElement element)
	{
		switch (element.ValueKind)
		{
		case JsonValueKind.Number:
		{
			int value;
			return (int)(element.TryGetInt32(out value) ? value : element.GetInt64());
		}
		case JsonValueKind.True:
			return 1;
		case JsonValueKind.False:
			return 0;
		case JsonValueKind.String:
		{
			long num = ParseIntegerText(element.GetString()) ?? throw new FormatException("'" + element.GetString() + "' is not an integer.");
			return (int)num;
		}
		default:
			throw new FormatException($"Expected a number, found {element.ValueKind}.");
		}
	}

	private static long ReadQword(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.Number => element.GetInt64(), 
			JsonValueKind.True => 1L, 
			JsonValueKind.False => 0L, 
			JsonValueKind.String => ParseIntegerText(element.GetString()) ?? throw new FormatException("'" + element.GetString() + "' is not an integer."), 
			_ => throw new FormatException($"Expected a number, found {element.ValueKind}."), 
		};
	}

	private static string ReadString(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString() ?? string.Empty, 
			JsonValueKind.Number => element.GetRawText(), 
			_ => throw new FormatException($"Expected a string, found {element.ValueKind}."), 
		};
	}

	private static string[] ReadMultiString(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.String)
		{
			return new string[1] { element.GetString() ?? string.Empty };
		}
		if (element.ValueKind != JsonValueKind.Array)
		{
			throw new FormatException($"Expected an array of strings, found {element.ValueKind}.");
		}
		return element.EnumerateArray().Select(delegate(JsonElement item)
		{
			if (item.ValueKind != JsonValueKind.String)
			{
				throw new FormatException("multiString entries must all be strings.");
			}
			return item.GetString() ?? string.Empty;
		}).ToArray();
	}

	private static byte[] ReadBinary(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Array)
		{
			return (from item in element.EnumerateArray()
				select (byte)item.GetInt32()).ToArray();
		}
		if (element.ValueKind != JsonValueKind.String)
		{
			throw new FormatException($"Expected a hex string or byte array, found {element.ValueKind}.");
		}
		return Convert.FromHexString(element.GetString().Replace(" ", string.Empty).Replace(",", string.Empty));
	}

	private static long? ParseIntegerText(string text)
	{
		text = text.Trim();
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			if (!long.TryParse(text2.Substring(2, text2.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
			{
				return null;
			}
			return result;
		}
		if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
		{
			return null;
		}
		return result2;
	}
}

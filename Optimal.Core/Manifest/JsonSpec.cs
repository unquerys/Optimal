using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Optimal.Core.Manifest;

public abstract class JsonSpec
{
	[JsonPropertyName("type")]
	public string Type { get; init; } = string.Empty;

	[JsonExtensionData]
	public Dictionary<string, JsonElement> Payload { get; init; } = new Dictionary<string, JsonElement>();

	protected abstract string SpecKind { get; }

	public JsonElement Require(string propertyName)
	{
		if (!Payload.TryGetValue(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
		{
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' is missing required property '{propertyName}'.");
		}
		return value;
	}

	public JsonElement? Optional(string propertyName)
	{
		if (!Payload.TryGetValue(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
		{
			return null;
		}
		return value;
	}

	public string RequireString(string propertyName)
	{
		JsonElement jsonElement = Require(propertyName);
		if (jsonElement.ValueKind != JsonValueKind.String)
		{
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' expects '{propertyName}' to be a string, found {jsonElement.ValueKind}.");
		}
		string? text = jsonElement.GetString();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' has an empty value for required property '{propertyName}'.");
		}
		return text;
	}

	public string? OptionalString(string propertyName)
	{
		JsonElement? jsonElement = Optional(propertyName);
		if (!jsonElement.HasValue)
		{
			return null;
		}
		if (jsonElement.Value.ValueKind != JsonValueKind.String)
		{
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' expects '{propertyName}' to be a string.");
		}
		return jsonElement.Value.GetString();
	}

	public bool OptionalBool(string propertyName, bool fallback)
	{
		return Optional(propertyName)?.ValueKind switch
		{
			null => fallback, 
			JsonValueKind.True => true, 
			JsonValueKind.False => false, 
			_ => throw new ManifestValidationException($"{SpecKind} of type '{Type}' expects '{propertyName}' to be a boolean."), 
		};
	}

	public int OptionalInt(string propertyName, int fallback)
	{
		JsonElement? jsonElement = Optional(propertyName);
		if (!jsonElement.HasValue)
		{
			return fallback;
		}
		if (jsonElement.Value.ValueKind != JsonValueKind.Number || !jsonElement.Value.TryGetInt32(out var value))
		{
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' expects '{propertyName}' to be a 32 bit integer.");
		}
		return value;
	}

	public TEnum OptionalEnum<TEnum>(string propertyName, TEnum fallback) where TEnum : struct, Enum
	{
		string text = OptionalString(propertyName);
		if (text == null)
		{
			return fallback;
		}
		if (!Enum.TryParse<TEnum>(text, ignoreCase: true, out var result))
		{
			string value = string.Join(", ", from n in Enum.GetNames<TEnum>()
				select n.ToLowerInvariant());
			throw new ManifestValidationException($"{SpecKind} of type '{Type}' has an unrecognised '{propertyName}' value '{text}'. Expected one of: {value}.");
		}
		return result;
	}
}

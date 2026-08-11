using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Optimal.Core.Manifest;

public sealed record ManifestFile
{
	[JsonPropertyName("schemaVersion")]
	public int SchemaVersion { get; init; } = 1;

	[JsonPropertyName("tweaks")]
	public IReadOnlyList<TweakDefinition> Tweaks { get; init; } = Array.Empty<TweakDefinition>();
}

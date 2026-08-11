using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Optimal.Core.Manifest;

public sealed record TweakDefinition
{
	[JsonPropertyName("id")]
	public required string Id { get; init; }

	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("category")]
	public required TweakCategory Category { get; init; }

	[JsonPropertyName("tier")]
	public required TweakTier Tier { get; init; }

	[JsonPropertyName("audience")]
	public TweakAudience Audience { get; init; }

	[JsonPropertyName("description")]
	public required string Description { get; init; }

	[JsonPropertyName("impact")]
	public string? Impact { get; init; }

	[JsonPropertyName("tradeoff")]
	public string? Tradeoff { get; init; }

	[JsonPropertyName("source")]
	public required string Source { get; init; }

	[JsonPropertyName("requires")]
	public Requirements Requires { get; init; } = Requirements.None;

	[JsonPropertyName("detect")]
	public IReadOnlyList<ConditionSpec> Detect { get; init; } = Array.Empty<ConditionSpec>();

	[JsonPropertyName("apply")]
	public IReadOnlyList<OperationSpec> Apply { get; init; } = Array.Empty<OperationSpec>();

	[JsonPropertyName("revert")]
	public IReadOnlyList<OperationSpec> Revert { get; init; } = Array.Empty<OperationSpec>();

	[JsonPropertyName("reboot")]
	public bool Reboot { get; init; }

	[JsonPropertyName("dependsOn")]
	public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

	[JsonPropertyName("conflictsWith")]
	public IReadOnlyList<string> ConflictsWith { get; init; } = Array.Empty<string>();

	[JsonIgnore]
	public string? SourceFile { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private TweakDefinition(TweakDefinition original)
	{
		Id = original.Id;
		Name = original.Name;
		Category = original.Category;
		Tier = original.Tier;
		Audience = original.Audience;
		Description = original.Description;
		Impact = original.Impact;
		Tradeoff = original.Tradeoff;
		Source = original.Source;
		Requires = original.Requires;
		Detect = original.Detect;
		Apply = original.Apply;
		Revert = original.Revert;
		Reboot = original.Reboot;
		DependsOn = original.DependsOn;
		ConflictsWith = original.ConflictsWith;
		SourceFile = original.SourceFile;
	}

	public TweakDefinition()
	{
	}
}

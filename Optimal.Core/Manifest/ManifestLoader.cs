using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Operations;

namespace Optimal.Core.Manifest;

public sealed class ManifestLoader
{
	public const int SupportedSchemaVersion = 1;

	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
	};

	private readonly OperationRegistry _registry;

	private readonly ILogger<ManifestLoader> _logger;

	private static Regex TweakIdRegex { get; } = new("^[a-z0-9]+(\\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant);

	public ManifestLoader(OperationRegistry registry, ILogger<ManifestLoader> logger)
	{
		_registry = registry;
		_logger = logger;
	}

	public async Task<TweakCatalog> LoadDirectoryAsync(string directory, CancellationToken cancellationToken)
	{
		if (!Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException("Manifest directory not found: " + directory);
		}
		List<string> files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Order<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (files.Count == 0)
		{
			throw new ManifestValidationException("No manifest files found under " + directory + ".");
		}
		List<TweakDefinition> all = new List<TweakDefinition>();
		foreach (string item in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<TweakDefinition> list = all;
			list.AddRange(await LoadFileAsync(item, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		TweakCatalog tweakCatalog = BuildCatalog(all);
		_logger.LogInformation("Loaded {TweakCount} tweaks from {FileCount} manifest files.", tweakCatalog.Tweaks.Count, files.Count);
		return tweakCatalog;
	}

	public async Task<IReadOnlyList<TweakDefinition>> LoadFileAsync(string path, CancellationToken cancellationToken)
	{
		ManifestFile file;
		await using (FileStream stream = File.OpenRead(path))
		{
			try
			{
				file = await JsonSerializer.DeserializeAsync<ManifestFile>(stream, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (JsonException ex)
			{
				throw new ManifestValidationException("Manifest is not valid JSON: " + ex.Message, ex)
				{
					SourceFile = path
				};
			}
		}
		if ((object)file == null)
		{
			throw new ManifestValidationException("Manifest file is empty.")
			{
				SourceFile = path
			};
		}
		if (file.SchemaVersion > 1)
		{
			throw new ManifestValidationException($"Manifest declares schemaVersion {file.SchemaVersion}, but this build understands at most {1}. Update Optimal.")
			{
				SourceFile = path
			};
		}
		List<TweakDefinition> list = new List<TweakDefinition>(file.Tweaks.Count);
		foreach (TweakDefinition tweak in file.Tweaks)
		{
			TweakDefinition tweakDefinition = tweak with
			{
				SourceFile = path
			};
			try
			{
				ValidateTweak(tweakDefinition);
			}
			catch (ManifestValidationException ex2)
			{
				throw new ManifestValidationException(ex2.Message, ex2)
				{
					SourceFile = path,
					TweakId = tweak.Id
				};
			}
			list.Add(tweakDefinition);
		}
		return list;
	}

	public IReadOnlyList<TweakDefinition> LoadFromJson(string json, string sourceName = "(inline)")
	{
		ManifestFile manifestFile;
		try
		{
			manifestFile = JsonSerializer.Deserialize<ManifestFile>(json, SerializerOptions);
		}
		catch (JsonException ex)
		{
			throw new ManifestValidationException("Manifest is not valid JSON: " + ex.Message, ex)
			{
				SourceFile = sourceName
			};
		}
		if ((object)manifestFile == null)
		{
			throw new ManifestValidationException("Manifest is empty.")
			{
				SourceFile = sourceName
			};
		}
		List<TweakDefinition> list = new List<TweakDefinition>(manifestFile.Tweaks.Count);
		using IEnumerator<TweakDefinition> enumerator = manifestFile.Tweaks.GetEnumerator();
		while (enumerator.MoveNext())
		{
			TweakDefinition tweakDefinition = enumerator.Current with
			{
				SourceFile = sourceName
			};
			ValidateTweak(tweakDefinition);
			list.Add(tweakDefinition);
		}
		return list;
	}

	public TweakCatalog BuildCatalog(IReadOnlyList<TweakDefinition> tweaks)
	{
		Dictionary<string, TweakDefinition> dictionary = new Dictionary<string, TweakDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (TweakDefinition tweak in tweaks)
		{
			if (!dictionary.TryAdd(tweak.Id, tweak))
			{
				TweakDefinition tweakDefinition = dictionary[tweak.Id];
				throw new ManifestValidationException($"Duplicate tweak id '{tweak.Id}' declared in both '{tweakDefinition.SourceFile}' and '{tweak.SourceFile}'.")
				{
					TweakId = tweak.Id
				};
			}
		}
		foreach (TweakDefinition tweak2 in tweaks)
		{
			foreach (string item in tweak2.DependsOn)
			{
				if (!dictionary.ContainsKey(item))
				{
					throw new ManifestValidationException($"Tweak '{tweak2.Id}' depends on '{item}', which no manifest defines.")
					{
						SourceFile = tweak2.SourceFile,
						TweakId = tweak2.Id
					};
				}
			}
			foreach (string item2 in tweak2.ConflictsWith)
			{
				if (!dictionary.ContainsKey(item2))
				{
					throw new ManifestValidationException($"Tweak '{tweak2.Id}' conflicts with '{item2}', which no manifest defines.")
					{
						SourceFile = tweak2.SourceFile,
						TweakId = tweak2.Id
					};
				}
			}
		}
		return new TweakCatalog(dictionary);
	}

	private void ValidateTweak(TweakDefinition tweak)
	{
		if (string.IsNullOrWhiteSpace(tweak.Id) || !TweakIdRegex.IsMatch(tweak.Id))
		{
			throw new ManifestValidationException("Tweak id '" + tweak.Id + "' must be lowercase dotted, for example 'gaming.gamedvr.disable'.");
		}
		RequireText(tweak.Name, "Name", tweak.Id);
		RequireText(tweak.Description, "Description", tweak.Id);
		RequireText(tweak.Source, "Source", tweak.Id);
		if (!Uri.TryCreate(tweak.Source, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && result.Scheme != Uri.UriSchemeHttp))
		{
			throw new ManifestValidationException($"Tweak '{tweak.Id}' must cite an http or https source, found '{tweak.Source}'.");
		}
		if (tweak.Apply.Count == 0)
		{
			throw new ManifestValidationException("Tweak '" + tweak.Id + "' has no apply operations.");
		}
		if (tweak.Revert.Count == 0)
		{
			throw new ManifestValidationException("Tweak '" + tweak.Id + "' has no revert operations. Every tweak must be reversible.");
		}
		if (tweak.Tier != TweakTier.Verified && string.IsNullOrWhiteSpace(tweak.Tradeoff))
		{
			throw new ManifestValidationException($"Tweak '{tweak.Id}' is tier '{tweak.Tier}' and must state its tradeoff in plain language.");
		}
		if (tweak.DependsOn.Contains<string>(tweak.Id, StringComparer.OrdinalIgnoreCase))
		{
			throw new ManifestValidationException("Tweak '" + tweak.Id + "' depends on itself.");
		}
		if (tweak.ConflictsWith.Contains<string>(tweak.Id, StringComparer.OrdinalIgnoreCase))
		{
			throw new ManifestValidationException("Tweak '" + tweak.Id + "' conflicts with itself.");
		}
		ValidateRequirements(tweak);
		foreach (ConditionSpec item in tweak.Detect)
		{
			_registry.GetCondition(item.Type).Validate(item);
		}
		foreach (OperationSpec item2 in tweak.Apply)
		{
			_registry.GetOperation(item2.Type).Validate(item2);
		}
		foreach (OperationSpec item3 in tweak.Revert)
		{
			_registry.GetOperation(item3.Type).Validate(item3);
		}
	}

	private static void ValidateRequirements(TweakDefinition tweak)
	{
		Requirements requires = tweak.Requires;
		if (requires.DeviceKind == DeviceKind.Unknown)
		{
			throw new ManifestValidationException("Tweak '" + tweak.Id + "' cannot require an unknown device kind. Use any, desktop, or laptop.");
		}
		int? minBuild = requires.MinBuild;
		if (minBuild.HasValue)
		{
			int valueOrDefault = minBuild.GetValueOrDefault();
			minBuild = requires.MaxBuild;
			if (minBuild.HasValue)
			{
				int valueOrDefault2 = minBuild.GetValueOrDefault();
				if (valueOrDefault > valueOrDefault2)
				{
					throw new ManifestValidationException($"Tweak '{tweak.Id}' has minBuild {valueOrDefault} greater than maxBuild {valueOrDefault2}.");
				}
			}
		}
		minBuild = requires.MinBuild;
		if (minBuild.HasValue)
		{
			int valueOrDefault3 = minBuild.GetValueOrDefault();
			if (valueOrDefault3 < 10240)
			{
				throw new ManifestValidationException($"Tweak '{tweak.Id}' has an implausible minBuild {valueOrDefault3}. Windows 10 starts at 10240.");
			}
		}
	}

	private static void RequireText(string? value, string field, string tweakId)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ManifestValidationException($"Tweak '{tweakId}' is missing required field '{field}'.");
		}
	}
}

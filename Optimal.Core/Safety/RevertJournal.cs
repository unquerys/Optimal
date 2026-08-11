using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Optimal.Core.Safety;

public sealed class RevertJournal
{
	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { (JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	private readonly OptimalPaths _paths;

	private readonly ILogger<RevertJournal> _logger;

	private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

	public RevertJournal(OptimalPaths paths, ILogger<RevertJournal> logger)
	{
		_paths = paths;
		_logger = logger;
	}

	public static string NewRunId(DateTimeOffset? now = null)
	{
		return (now ?? DateTimeOffset.UtcNow).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
	}

	public async Task SaveAsync(RunRecord record, CancellationToken cancellationToken)
	{
		_paths.EnsureCreated();
		string destination = _paths.JournalFileForRun(record.RunId);
		await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			string temporary = destination + ".tmp";
			await using (FileStream stream = File.Create(temporary))
			{
				await JsonSerializer.SerializeAsync((Stream)stream, record, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			File.Move(temporary, destination, overwrite: true);
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public async Task<RunRecord?> LoadAsync(string runId, CancellationToken cancellationToken)
	{
		string path = _paths.JournalFileForRun(runId);
		return (!File.Exists(path)) ? null : (await ReadAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	public async Task<IReadOnlyList<RunRecord>> ListAsync(int limit, CancellationToken cancellationToken)
	{
		if (limit <= 0)
		{
			return Array.Empty<RunRecord>();
		}
		if (!Directory.Exists(_paths.Journal))
		{
			return Array.Empty<RunRecord>();
		}
		IEnumerable<string> enumerable = Directory.EnumerateFiles(_paths.Journal, "*.json").OrderByDescending<string, string>((string f) => f, StringComparer.Ordinal).Take(limit);
		List<RunRecord> records = new List<RunRecord>();
		foreach (string file in enumerable)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				RunRecord runRecord = await ReadAsync(file, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (runRecord != null)
				{
					records.Add(runRecord);
				}
			}
			catch (Exception ex) when (((ex is JsonException || ex is IOException) ? 1 : 0) != 0)
			{
				_logger.LogWarning(ex, "Skipping unreadable journal file {File}", file);
			}
		}
		return records;
	}

	public async Task<RunRecord?> FindLatestRevertableAsync(CancellationToken cancellationToken)
	{
		return (await ListAsync(50, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault((RunRecord r) => r.CanRevert);
	}

	private static async Task<RunRecord?> ReadAsync(string path, CancellationToken cancellationToken)
	{
		RunRecord result;
		await using (FileStream stream = File.OpenRead(path))
		{
			result = await JsonSerializer.DeserializeAsync<RunRecord>(stream, SerializerOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return result;
	}
}

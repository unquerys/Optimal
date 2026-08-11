using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Optimal.Core.Operations;
using Optimal.Core.Safety;
using Xunit;

namespace Optimal.Tests;

public sealed class RevertJournalTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "Optimal.Tests", Guid.NewGuid().ToString("N"));

	private readonly RevertJournal _journal;

	public RevertJournalTests()
	{
		_journal = new RevertJournal(new OptimalPaths(_root), NullLogger<RevertJournal>.Instance);
	}

	[Fact]
	public async Task Save_and_load_round_trip_polymorphic_backup_state()
	{
		RunRecord record = Record("20260101-010101-aaaaaa", ExecutionMode.Apply, applied: true, withBackup: true);
		await _journal.SaveAsync(record, CancellationToken.None);
		RunRecord runRecord = await _journal.LoadAsync(record.RunId, CancellationToken.None);
		Assert.NotNull(runRecord);
		Assert.Equal(record.RunId, runRecord.RunId);
		RegistryValueBackup registryValueBackup = Assert.IsType<RegistryValueBackup>(Assert.Single(Assert.Single(runRecord.Tweaks).Operations).Backups.Single());
		Assert.Equal("Before", registryValueBackup.Data);
		Assert.True(runRecord.CanRevert);
	}

	[Fact]
	public async Task ListAsync_returns_newest_first_and_skips_corrupt_records()
	{
		await _journal.SaveAsync(Record("20260101-010101-aaaaaa", ExecutionMode.Apply), CancellationToken.None);
		await _journal.SaveAsync(Record("20260102-010101-bbbbbb", ExecutionMode.DryRun), CancellationToken.None);
		await File.WriteAllTextAsync(Path.Combine(_root, "journal", "20260103-010101-cccccc.json"), "{not json");
		IReadOnlyList<RunRecord> source = await _journal.ListAsync(10, CancellationToken.None);
		Assert.Equal(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "20260102-010101-bbbbbb", "20260101-010101-aaaaaa" }), source.Select((RunRecord record) => record.RunId));
	}

	[Fact]
	public async Task FindLatestRevertable_ignores_dry_runs_and_no_change_runs()
	{
		await _journal.SaveAsync(Record("20260101-010101-aaaaaa", ExecutionMode.Apply), CancellationToken.None);
		await _journal.SaveAsync(Record("20260102-010101-bbbbbb", ExecutionMode.Apply, applied: false), CancellationToken.None);
		await _journal.SaveAsync(Record("20260103-010101-cccccc", ExecutionMode.DryRun), CancellationToken.None);
		RunRecord runRecord = await _journal.FindLatestRevertableAsync(CancellationToken.None);
		Assert.NotNull(runRecord);
		Assert.Equal("20260101-010101-aaaaaa", runRecord.RunId);
	}

	[Fact]
	public void CanRevert_accepts_declared_revert_fallback_without_captured_state()
	{
		Assert.True(Record("20260101-010101-aaaaaa", ExecutionMode.Apply).CanRevert);
	}

	[Fact]
	public async Task LoadAsync_rejects_path_traversal_run_ids()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => _journal.LoadAsync("..\\outside", CancellationToken.None));
	}

	private static RunRecord Record(string id, ExecutionMode mode, bool applied = true, bool withBackup = false)
	{
		OperationRunRecord operationRunRecord = new OperationRunRecord
		{
			Type = "registry",
			Describe = "Test operation",
			Outcome = (applied ? StepOutcome.Applied : StepOutcome.Skipped)
		};
		if (withBackup)
		{
			operationRunRecord.Backups.Add(new RegistryValueBackup
			{
				Hive = "HKCU",
				SubKey = "Software\\Optimal.Tests",
				Name = "Value",
				Existed = true,
				KeyExisted = true,
				ValueType = "String",
				Data = "Before",
				Describe = "Value was Before"
			});
		}
		return new RunRecord
		{
			RunId = id,
			StartedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
			CompletedUtc = DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
			Mode = mode,
			AppVersion = "test",
			Tweaks = new List<TweakRunRecord>
			{
				new()
				{
					TweakId = "system.test",
					Name = "Test",
					Outcome = applied ? StepOutcome.Applied : StepOutcome.Skipped,
					Operations = new List<OperationRunRecord> { operationRunRecord }
				}
			}
		};
	}

	public void Dispose()
	{
		if (Directory.Exists(_root))
		{
			Directory.Delete(_root, recursive: true);
		}
	}
}

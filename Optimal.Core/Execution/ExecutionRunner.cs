using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Optimal.Core.Safety;

namespace Optimal.Core.Execution;

public sealed class ExecutionRunner
{
	private readonly OperationRegistry _registry;

	private readonly RevertJournal _journal;

	private readonly RegistryBackupService _registryBackup;

	private readonly SystemRestorePointService _restorePoints;

	private readonly OptimalPaths _paths;

	private readonly IProcessRunner _process;

	private readonly ILogger<ExecutionRunner> _logger;

	private static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

	public ExecutionRunner(OperationRegistry registry, RevertJournal journal, RegistryBackupService registryBackup, SystemRestorePointService restorePoints, OptimalPaths paths, IProcessRunner process, ILogger<ExecutionRunner> logger)
	{
		_registry = registry;
		_journal = journal;
		_registryBackup = registryBackup;
		_restorePoints = restorePoints;
		_paths = paths;
		_process = process;
		_logger = logger;
	}

	public async Task<RunRecord> ApplyAsync(ExecutionPlan plan, RunOptions options, IProgress<ExecutionProgress>? progress, CancellationToken cancellationToken)
	{
		_paths.EnsureCreated();
		string runId = RevertJournal.NewRunId();
		RunRecord record = new RunRecord
		{
			RunId = runId,
			StartedUtc = DateTimeOffset.UtcNow,
			Mode = (options.DryRun ? ExecutionMode.DryRun : ExecutionMode.Apply),
			AppVersion = AppVersion,
			PresetName = options.PresetName
		};
		OperationContext context = new OperationContext
		{
			Logger = _logger,
			Process = _process,
			DryRun = options.DryRun
		};
		int total = plan.Tweaks.Count;
		await PrepareSafetyNetAsync(plan, options, record, runId, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		int completed = 0;
		foreach (PlannedTweak tweak2 in plan.Tweaks)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TweakDefinition tweak = tweak2.Tweak;
			progress?.Report(new ExecutionProgress
			{
				Phase = ExecutionPhase.ApplyingTweaks,
				TweakId = tweak.Id,
				TweakName = tweak.Name,
				Outcome = StepOutcome.Running,
				Completed = completed,
				Total = total,
				Message = "Applying " + tweak.Name
			});
			TweakRunRecord tweakRecord = new TweakRunRecord
			{
				TweakId = tweak.Id,
				Name = tweak.Name,
				RebootRequired = tweak.Reboot
			};
			record.Tweaks.Add(tweakRecord);
			if (!tweak2.AlreadyApplied)
			{
				await RunTweakAsync(tweak, tweakRecord, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				tweakRecord.Outcome = StepOutcome.Skipped;
				_logger.LogDebug("Skipping {TweakId}, already in the desired state.", tweak.Id);
			}
			completed++;
			progress?.Report(new ExecutionProgress
			{
				Phase = ExecutionPhase.ApplyingTweaks,
				TweakId = tweak.Id,
				TweakName = tweak.Name,
				Outcome = tweakRecord.Outcome,
				Completed = completed,
				Total = total,
				Message = (tweakRecord.Error ?? $"{tweak.Name}: {tweakRecord.Outcome}")
			});
			await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		record.CompletedUtc = DateTimeOffset.UtcNow;
		await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		progress?.Report(new ExecutionProgress
		{
			Phase = ExecutionPhase.Finishing,
			Completed = total,
			Total = total,
			Message = $"Applied {record.AppliedCount}, skipped {record.SkippedCount}, failed {record.FailedCount}."
		});
		_logger.LogInformation("Run {RunId} finished: {Applied} applied, {Skipped} skipped, {Failed} failed.", runId, record.AppliedCount, record.SkippedCount, record.FailedCount);
		return record;
	}

	public async Task<RunRecord> RevertAsync(RunRecord original, TweakCatalog catalog, IProgress<ExecutionProgress>? progress, CancellationToken cancellationToken)
	{
		_paths.EnsureCreated();
		RunRecord record = new RunRecord
		{
			RunId = RevertJournal.NewRunId(),
			StartedUtc = DateTimeOffset.UtcNow,
			Mode = ExecutionMode.Revert,
			AppVersion = AppVersion,
			PresetName = "Undo of " + original.RunId,
			RevertsRunId = original.RunId
		};
		OperationContext context = new OperationContext
		{
			Logger = _logger,
			Process = _process,
			DryRun = false
		};
		List<TweakRunRecord> applied = original.Tweaks.Where((TweakRunRecord t) => t.Outcome == StepOutcome.Applied).Reverse().ToList();
		int completed = 0;
		progress?.Report(new ExecutionProgress
		{
			Phase = ExecutionPhase.CreatingRestorePoint,
			Total = applied.Count,
			Message = "Creating a restore point before undo"
		});
		RestorePointResult restorePoint = await _restorePoints.CreateAsync("Optimal undo " + original.RunId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		record.RestorePointStatus = restorePoint.Status;
		await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!restorePoint.Created)
		{
			throw new InvalidOperationException("Optimal did not change anything because the required restore point could not be created. " + restorePoint.Message);
		}
		foreach (TweakRunRecord tweakRecord in applied)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TweakRunRecord undoRecord = new TweakRunRecord
			{
				TweakId = tweakRecord.TweakId,
				Name = tweakRecord.Name
			};
			record.Tweaks.Add(undoRecord);
			progress?.Report(new ExecutionProgress
			{
				Phase = ExecutionPhase.ApplyingTweaks,
				TweakId = tweakRecord.TweakId,
				TweakName = tweakRecord.Name,
				Outcome = StepOutcome.Running,
				Completed = completed,
				Total = applied.Count,
				Message = "Undoing " + tweakRecord.Name
			});
			try
			{
				await RevertTweakAsync(tweakRecord, undoRecord, catalog, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				undoRecord.Outcome = StepOutcome.Failed;
				undoRecord.Error = ex.Message;
				_logger.LogError(ex, "Could not undo {TweakId}.", tweakRecord.TweakId);
			}
			completed++;
			progress?.Report(new ExecutionProgress
			{
				Phase = ExecutionPhase.ApplyingTweaks,
				TweakId = tweakRecord.TweakId,
				TweakName = tweakRecord.Name,
				Outcome = undoRecord.Outcome,
				Completed = completed,
				Total = applied.Count,
				Message = (undoRecord.Error ?? $"{tweakRecord.Name}: {undoRecord.Outcome}")
			});
			await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		record.CompletedUtc = DateTimeOffset.UtcNow;
		await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return record;
	}

	private async Task PrepareSafetyNetAsync(ExecutionPlan plan, RunOptions options, RunRecord record, string runId, IProgress<ExecutionProgress>? progress, CancellationToken cancellationToken)
	{
		if (options.DryRun)
		{
			return;
		}
		progress?.Report(new ExecutionProgress
		{
			Phase = ExecutionPhase.CreatingRestorePoint,
			Total = plan.Tweaks.Count,
			Message = "Creating a system restore point"
		});
		RestorePointResult result = await _restorePoints.CreateAsync("Optimal " + (options.PresetName ?? "run"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		record.RestorePointStatus = result.Status;
		progress?.Report(new ExecutionProgress
		{
			Phase = ExecutionPhase.CreatingRestorePoint,
			Total = plan.Tweaks.Count,
			Message = result.Message
		});
		if (!result.Created)
		{
			await _journal.SaveAsync(record, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			throw new InvalidOperationException("Optimal did not change anything because the required restore point could not be created. " + result.Message);
		}
		if (options.BackupRegistry)
		{
			progress?.Report(new ExecutionProgress
			{
				Phase = ExecutionPhase.BackingUpRegistry,
				Total = plan.Tweaks.Count,
				Message = "Backing up affected registry keys"
			});
			string directory = _paths.BackupDirectoryForRun(runId);
			IEnumerable<OperationSpec> operations = plan.Tweaks.SelectMany((PlannedTweak t) => t.Tweak.Apply);
			if ((await _registryBackup.ExportAsync(operations, directory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Count > 0)
			{
				record.RegistryBackupDirectory = directory;
			}
		}
	}

	private async Task RunTweakAsync(TweakDefinition tweak, TweakRunRecord tweakRecord, OperationContext context, CancellationToken cancellationToken)
	{
		foreach (OperationSpec operation in tweak.Apply)
		{
			cancellationToken.ThrowIfCancellationRequested();
			IOperationHandler handler;
			string description;
			try
			{
				handler = _registry.GetOperation(operation.Type);
				description = handler.Describe(operation);
			}
			catch (Exception ex)
			{
				tweakRecord.Outcome = StepOutcome.Failed;
				tweakRecord.Error = ex.Message;
				_logger.LogError(ex, "Tweak {TweakId} references an operation we cannot run.", tweak.Id);
				return;
			}
			OperationRunRecord operationRecord = new OperationRunRecord
			{
				Type = operation.Type,
				Describe = description
			};
			tweakRecord.Operations.Add(operationRecord);
			try
			{
				IReadOnlyList<BackupEntry> collection = await handler.CaptureAsync(operation, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				operationRecord.Backups.AddRange(collection);
				await handler.ExecuteAsync(operation, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				operationRecord.Outcome = (context.DryRun ? StepOutcome.Skipped : StepOutcome.Applied);
			}
			catch (OperationCanceledException)
			{
				operationRecord.Outcome = StepOutcome.Failed;
				operationRecord.Error = "Cancelled.";
				throw;
			}
			catch (Exception ex3)
			{
				operationRecord.Outcome = StepOutcome.Failed;
				operationRecord.Error = ex3.Message;
				tweakRecord.Outcome = StepOutcome.Failed;
				tweakRecord.Error = description + " failed: " + ex3.Message;
				_logger.LogError(ex3, "Operation failed in tweak {TweakId}: {Description}", tweak.Id, description);
				return;
			}
		}
		tweakRecord.Outcome = (context.DryRun ? StepOutcome.Skipped : StepOutcome.Applied);
	}

	private async Task RevertTweakAsync(TweakRunRecord original, TweakRunRecord undoRecord, TweakCatalog catalog, OperationContext context, CancellationToken cancellationToken)
	{
		List<BackupEntry> list = original.Operations.Where((OperationRunRecord o) => o.Outcome == StepOutcome.Applied).SelectMany((OperationRunRecord o) => o.Backups).Reverse()
			.ToList();
		if (list.Count > 0)
		{
			foreach (BackupEntry item in list)
			{
				OperationRunRecord record = new OperationRunRecord
				{
					Type = "restore",
					Describe = "Restore: " + item.Describe
				};
				undoRecord.Operations.Add(record);
				await _registry.GetRestorer(item).RestoreAsync(item, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				record.Outcome = StepOutcome.Applied;
			}
			undoRecord.Outcome = StepOutcome.Applied;
			return;
		}
		if (!catalog.TryGet(original.TweakId, out TweakDefinition tweak))
		{
			undoRecord.Outcome = StepOutcome.Failed;
			undoRecord.Error = "No captured state for '" + original.TweakId + "' and it is no longer in the catalog, so it cannot be undone.";
			return;
		}
		_logger.LogInformation("No captured state for {TweakId}, falling back to its declared revert.", original.TweakId);
		foreach (OperationSpec item2 in tweak.Revert)
		{
			IOperationHandler operation = _registry.GetOperation(item2.Type);
			OperationRunRecord record = new OperationRunRecord
			{
				Type = item2.Type,
				Describe = operation.Describe(item2)
			};
			undoRecord.Operations.Add(record);
			await operation.ExecuteAsync(item2, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			record.Outcome = StepOutcome.Applied;
		}
		undoRecord.Outcome = StepOutcome.Applied;
	}
}

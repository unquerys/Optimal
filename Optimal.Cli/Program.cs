using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Detection;
using Optimal.Core.Execution;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Optimal.Core.Safety;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Optimal.Cli;

internal static class Program
{
	private sealed class ProbeReporter : IProgress<ProbeProgress>
	{
		public void Report(ProbeProgress value)
		{
			Console.WriteLine($"[{value.Completed}/{value.Total}] {value.Stage}");
		}
	}

	private sealed class ExecutionReporter : IProgress<ExecutionProgress>
	{
		public void Report(ExecutionProgress value)
		{
			Console.WriteLine(((value.Total == 0) ? string.Empty : $"[{value.Completed}/{value.Total}] ") + value.Message);
		}
	}

	private static async Task<int> Main(string[] args)
	{
		if (args.Length == 0 || HasOption(args, "--help") || HasOption(args, "-h"))
		{
			PrintHelp();
			return 0;
		}
		OptimalPaths optimalPaths = new OptimalPaths(GetOption(args, "--data-dir"));
		optimalPaths.EnsureCreated();
		Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console(LogEventLevel.Verbose, "[{Level:u3}] {Message:lj}{NewLine}{Exception}").WriteTo.File(Path.Combine(optimalPaths.Logs, "optimal-.log"), LogEventLevel.Verbose, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null, retainedFileCountLimit: 14, fileSizeLimitBytes: 1073741824L, levelSwitch: null, buffered: false, shared: false, flushToDiskInterval: null, rollingInterval: RollingInterval.Day).CreateLogger();
		using SerilogLoggerFactory loggerFactory = new SerilogLoggerFactory(logger, dispose: true);
		CancellationTokenSource cancellation = new CancellationTokenSource();
		try
		{
			Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs eventArgs)
			{
				eventArgs.Cancel = true;
				cancellation.Cancel();
			};
			try
			{
				return await ExecuteAsync(args, optimalPaths, loggerFactory, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
				Console.Error.WriteLine("Cancelled.");
				return 130;
			}
			catch (Exception ex2)
			{
				loggerFactory.CreateLogger("Optimal.Cli").LogError(ex2, "Command failed: {Message}", ex2.Message);
				return 1;
			}
		}
		finally
		{
			if (cancellation != null)
			{
				((IDisposable)cancellation).Dispose();
			}
		}
	}

	private static async Task<int> ExecuteAsync(string[] args, OptimalPaths paths, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
	{
		string command = args[0].ToLowerInvariant();
		ProcessRunner process = new ProcessRunner();
		OperationRegistry registry = OperationRegistry.CreateDefault(process);
		if (command == "profile")
		{
			PrintProfile(await new MachineProfiler(loggerFactory.CreateLogger<MachineProfiler>()).ProfileAsync(new ProbeReporter(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			return 0;
		}
		RevertJournal journal = new RevertJournal(paths, loggerFactory.CreateLogger<RevertJournal>());
		if (command == "history")
		{
			int result;
			int limit = ((int.TryParse(GetOption(args, "--limit"), out result) && result > 0) ? result : 20);
			PrintHistory(await journal.ListAsync(limit, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			return 0;
		}
		bool flag;
		switch (command)
		{
		case "validate":
		case "plan":
		case "run":
		case "revert":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			Console.Error.WriteLine("Unknown command '" + args[0] + "'.\n");
			PrintHelp();
			return 2;
		}
		string manifestPath = Positional(args, 1) ?? throw new ArgumentException("The " + command + " command needs a manifest file or directory.");
		TweakCatalog catalog = await LoadCatalogAsync(new ManifestLoader(registry, loggerFactory.CreateLogger<ManifestLoader>()), manifestPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (command == "validate")
		{
			Console.WriteLine($"Valid: {catalog.Count} tweak(s) loaded from {Path.GetFullPath(manifestPath)}");
			return 0;
		}
		if (command == "revert")
		{
			if (!HasOption(args, "--yes"))
			{
				Console.Error.WriteLine("Revert writes to the machine. Re-run with --yes after reviewing history.");
				return 2;
			}
			string text = Positional(args, 2);
			RunRecord runRecord = ((text != null && !text.Equals("latest", StringComparison.OrdinalIgnoreCase)) ? (await journal.LoadAsync(text, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : (await journal.FindLatestRevertableAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));
			RunRecord runRecord2 = runRecord;
			if (runRecord2 == null)
			{
				Console.Error.WriteLine("No matching revertable run was found.");
				return 3;
			}
			RunRecord obj = await CreateRunner(registry, journal, paths, process, loggerFactory).RevertAsync(runRecord2, catalog, new ExecutionReporter(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			PrintRunSummary(obj);
			return (obj.FailedCount != 0) ? 4 : 0;
		}
		MachineProfile profile = await new MachineProfiler(loggerFactory.CreateLogger<MachineProfiler>()).ProfileAsync(null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		IReadOnlyList<TweakDefinition> selection = ResolveSelection(catalog, GetOption(args, "--only"));
		OperationContext context = new OperationContext
		{
			Logger = loggerFactory.CreateLogger("Optimal.Planning"),
			Process = process,
			DryRun = true
		};
		ExecutionPlan plan = await new ExecutionPlanner(registry, loggerFactory.CreateLogger<ExecutionPlanner>()).PlanAsync(selection, profile, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		PrintPlan(plan);
		if (command == "plan")
		{
			return 0;
		}
		bool flag2 = HasOption(args, "--apply");
		if (flag2 && !HasOption(args, "--yes"))
		{
			Console.Error.WriteLine("Nothing was changed. To apply this reviewed plan, add --apply --yes.");
			return 2;
		}
		RunRecord obj2 = await CreateRunner(registry, journal, paths, process, loggerFactory).ApplyAsync(plan, new RunOptions
		{
			DryRun = !flag2,
			BackupRegistry = !HasOption(args, "--no-registry-backup"),
			PresetName = (GetOption(args, "--name") ?? Path.GetFileNameWithoutExtension(manifestPath))
		}, new ExecutionReporter(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		PrintRunSummary(obj2);
		return (obj2.FailedCount != 0) ? 4 : 0;
	}

	private static ExecutionRunner CreateRunner(OperationRegistry registry, RevertJournal journal, OptimalPaths paths, IProcessRunner process, ILoggerFactory loggerFactory)
	{
		return new ExecutionRunner(registry, journal, new RegistryBackupService(process, loggerFactory.CreateLogger<RegistryBackupService>()), new SystemRestorePointService(loggerFactory.CreateLogger<SystemRestorePointService>()), paths, process, loggerFactory.CreateLogger<ExecutionRunner>());
	}

	private static async Task<TweakCatalog> LoadCatalogAsync(ManifestLoader loader, string path, CancellationToken cancellationToken)
	{
		if (Directory.Exists(path))
		{
			return await loader.LoadDirectoryAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Manifest path was not found.", path);
		}
		return loader.BuildCatalog(await loader.LoadFileAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static IReadOnlyList<TweakDefinition> ResolveSelection(TweakCatalog catalog, string? only)
	{
		if (string.IsNullOrWhiteSpace(only))
		{
			return catalog.Tweaks;
		}
		string[] ids = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (!catalog.TryResolve(ids, out IReadOnlyList<TweakDefinition> resolved, out IReadOnlyList<string> unknown))
		{
			throw new ArgumentException("Unknown tweak id(s): " + string.Join(", ", unknown));
		}
		return resolved;
	}

	private static void PrintProfile(MachineProfile profile)
	{
		Console.WriteLine($"OS:       {profile.OsName} {profile.Edition}, build {profile.Build} {profile.DisplayVersion}");
		Console.WriteLine($"Device:   {profile.DeviceKind}, elevated: {profile.IsElevated}");
		Console.WriteLine($"CPU:      {profile.CpuName} ({profile.CpuPhysicalCores} cores / {profile.CpuLogicalCores} threads)");
		Console.WriteLine($"GPU:      {profile.GpuName} ({profile.GpuVendor})");
		Console.WriteLine($"Memory:   {profile.RamGigabytes} GB");
		Console.WriteLine($"Storage:  {profile.SystemDriveKind}");
		Console.WriteLine("Features: " + string.Join(", ", profile.Capabilities.Order<string>(StringComparer.OrdinalIgnoreCase)));
	}

	private static void PrintPlan(ExecutionPlan plan)
	{
		Console.WriteLine($"Plan: {plan.ChangeCount} change(s), {plan.Tweaks.Count} eligible, {plan.Excluded.Count} excluded");
		foreach (PlannedTweak tweak in plan.Tweaks)
		{
			Console.WriteLine($"  [{tweak.CurrentState}] {tweak.Tweak.Id}  -  {tweak.Tweak.Name}");
			foreach (string description in tweak.Descriptions)
			{
				Console.WriteLine("      " + description);
			}
		}
		foreach (ExcludedTweak item in plan.Excluded)
		{
			Console.WriteLine($"  [Excluded: {item.Reason}] {item.Tweak.Id}  -  {item.Explanation}");
		}
	}

	private static void PrintHistory(IReadOnlyList<RunRecord> records)
	{
		if (records.Count == 0)
		{
			Console.WriteLine("No runs have been recorded.");
			return;
		}
		foreach (RunRecord record in records)
		{
			Console.WriteLine($"{record.RunId}  {record.Mode,-6}  applied={record.AppliedCount} skipped={record.SkippedCount} failed={record.FailedCount} revertable={record.CanRevert}  {record.PresetName}");
		}
	}

	private static void PrintRunSummary(RunRecord record)
	{
		Console.WriteLine();
		Console.WriteLine($"Run {record.RunId} ({record.Mode})");
		Console.WriteLine($"Applied: {record.AppliedCount}, skipped: {record.SkippedCount}, failed: {record.FailedCount}");
		Console.WriteLine("Journal: " + record.RunId + ".json");
		if (record.RebootRequired)
		{
			Console.WriteLine("A restart is required for all changes to take effect.");
		}
	}

	private static bool HasOption(IEnumerable<string> args, string name)
	{
		return args.Any((string arg) => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	private static string? GetOption(IReadOnlyList<string> args, string name)
	{
		for (int i = 0; i < args.Count - 1; i++)
		{
			if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return args[i + 1];
			}
		}
		return null;
	}

	private static string? Positional(IReadOnlyList<string> args, int position)
	{
		int num = 0;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--data-dir", "--only", "--name", "--limit" };
		for (int i = 0; i < args.Count; i++)
		{
			if (hashSet.Contains(args[i]))
			{
				i++;
			}
			else if (!args[i].StartsWith('-') && num++ == position)
			{
				return args[i];
			}
		}
		return null;
	}

	private static void PrintHelp()
	{
		Console.WriteLine("Optimal engine console harness");
		Console.WriteLine();
		Console.WriteLine("  optimal-cli profile [--data-dir PATH]");
		Console.WriteLine("  optimal-cli validate MANIFEST");
		Console.WriteLine("  optimal-cli plan MANIFEST [--only id,id]");
		Console.WriteLine("  optimal-cli run MANIFEST [--only id,id] [--apply --yes]");
		Console.WriteLine("  optimal-cli history [--limit N]");
		Console.WriteLine("  optimal-cli revert MANIFEST [RUN_ID|latest] --yes");
		Console.WriteLine();
		Console.WriteLine("run is a dry run unless both --apply and --yes are supplied.");
		Console.WriteLine("Applied runs require a new restore point. Use --no-registry-backup only if needed.");
		Console.WriteLine("All commands accept --data-dir PATH.");
	}
}

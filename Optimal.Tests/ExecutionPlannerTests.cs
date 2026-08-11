using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Optimal.Core.Detection;
using Optimal.Core.Execution;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Xunit;

namespace Optimal.Tests;

public sealed class ExecutionPlannerTests
{
	private sealed class NoOpHandler : IOperationHandler
	{
		public string Type => "noop";

		public void Validate(OperationSpec spec)
		{
		}

		public string Describe(OperationSpec spec)
		{
			return "Do nothing";
		}

		public Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult((IReadOnlyList<BackupEntry>)Array.Empty<BackupEntry>());
		}

		public Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class BooleanConditionHandler : IConditionHandler
	{
		public string Type => "boolean";

		public void Validate(ConditionSpec spec)
		{
		}

		public string Describe(ConditionSpec spec)
		{
			return "Boolean test";
		}

		public Task<bool> EvaluateAsync(ConditionSpec spec, OperationContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(spec.Require("matches").GetBoolean());
		}
	}

	private sealed class FakeProcessRunner : IProcessRunner
	{
		public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
		{
			return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
		}
	}

	private static readonly MachineProfile Desktop = new MachineProfile
	{
		OsName = "Windows 11 Pro",
		Edition = "Pro",
		Build = 22631,
		DisplayVersion = "23H2",
		IsWindows11 = true,
		DeviceKind = DeviceKind.Desktop,
		CpuName = "Test CPU",
		CpuPhysicalCores = 8,
		CpuLogicalCores = 16,
		GpuName = "Test GPU",
		GpuVendor = GpuVendor.Nvidia,
		RamGigabytes = 32,
		SystemDriveKind = StorageKind.Nvme,
		IsElevated = true,
		Capabilities = new HashSet<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "hags", "x64" }), StringComparer.OrdinalIgnoreCase)
	};

	private readonly ExecutionPlanner _planner;

	private readonly OperationContext _context;

	public ExecutionPlannerTests()
	{
		OperationRegistry registry = new OperationRegistry(new global::_003C_003Ez__ReadOnlySingleElementList<IOperationHandler>(new NoOpHandler()), new global::_003C_003Ez__ReadOnlySingleElementList<IConditionHandler>(new BooleanConditionHandler()));
		_planner = new ExecutionPlanner(registry, NullLogger<ExecutionPlanner>.Instance);
		_context = new OperationContext
		{
			Logger = NullLogger.Instance,
			Process = new FakeProcessRunner(),
			DryRun = true
		};
	}

	[Fact]
	public async Task PlanAsync_orders_dependencies_before_dependents()
	{
		TweakDefinition prerequisite = Tweak("system.base");
		TweakDefinition dependent = Tweak("system.child", null, new global::_003C_003Ez__ReadOnlySingleElementList<string>(prerequisite.Id));
		ExecutionPlan executionPlan = await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlyArray<TweakDefinition>(new TweakDefinition[2] { dependent, prerequisite }), Desktop, _context, CancellationToken.None);
		Assert.Equal(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { prerequisite.Id, dependent.Id }), executionPlan.Tweaks.Select((PlannedTweak t) => t.Tweak.Id));
	}

	[Fact]
	public async Task PlanAsync_excludes_dependents_when_a_prerequisite_is_ineligible()
	{
		TweakDefinition prerequisite = Tweak("system.future", new Requirements
		{
			MinBuild = 99999
		});
		TweakDefinition dependent = Tweak("system.child", null, new global::_003C_003Ez__ReadOnlySingleElementList<string>(prerequisite.Id));
		ExecutionPlan obj = await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlyArray<TweakDefinition>(new TweakDefinition[2] { dependent, prerequisite }), Desktop, _context, CancellationToken.None);
		Assert.Empty(obj.Tweaks);
		Assert.Contains((IEnumerable<ExcludedTweak>)obj.Excluded, (Predicate<ExcludedTweak>)((ExcludedTweak item) => item.Tweak.Id == prerequisite.Id && item.Reason == ExclusionReason.UnsupportedBuild));
		Assert.Contains((IEnumerable<ExcludedTweak>)obj.Excluded, (Predicate<ExcludedTweak>)((ExcludedTweak item) => item.Tweak.Id == dependent.Id && item.Reason == ExclusionReason.MissingDependency));
	}

	[Fact]
	public async Task PlanAsync_excludes_a_dependency_that_was_not_selected()
	{
		TweakDefinition item = Tweak("system.child", null, new global::_003C_003Ez__ReadOnlySingleElementList<string>("system.base"));
		ExcludedTweak excludedTweak = Assert.Single((await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlySingleElementList<TweakDefinition>(item), Desktop, _context, CancellationToken.None)).Excluded);
		Assert.Equal(ExclusionReason.MissingDependency, excludedTweak.Reason);
		Assert.Contains("system.base", excludedTweak.Explanation);
	}

	[Fact]
	public async Task PlanAsync_does_not_treat_failed_device_detection_as_desktop()
	{
		TweakDefinition item = Tweak("system.desktop-only", new Requirements
		{
			DeviceKind = DeviceKind.Desktop
		});
		ExcludedTweak excludedTweak = Assert.Single((await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlySingleElementList<TweakDefinition>(item), Desktop with
		{
			DeviceKind = DeviceKind.Unknown
		}, _context, CancellationToken.None)).Excluded);
		Assert.Equal(ExclusionReason.WrongDeviceKind, excludedTweak.Reason);
	}

	[Fact]
	public async Task PlanAsync_removes_both_sides_of_a_conflict()
	{
		TweakDefinition tweakDefinition = Tweak("power.first", null, null, new global::_003C_003Ez__ReadOnlySingleElementList<string>("power.second"));
		TweakDefinition tweakDefinition2 = Tweak("power.second");
		ExecutionPlan executionPlan = await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlyArray<TweakDefinition>(new TweakDefinition[2] { tweakDefinition, tweakDefinition2 }), Desktop, _context, CancellationToken.None);
		Assert.Empty(executionPlan.Tweaks);
		Assert.Equal(2, executionPlan.Excluded.Count((ExcludedTweak item) => item.Reason == ExclusionReason.ConflictsWithSelection));
	}

	[Fact]
	public async Task DetectAsync_reports_partial_when_only_some_conditions_match()
	{
		TweakDefinition tweak = Tweak("system.detect", null, null, null, new global::_003C_003Ez__ReadOnlyArray<ConditionSpec>(new ConditionSpec[2]
		{
			Condition(matches: true),
			Condition(matches: false)
		}));
		Assert.Equal(TweakState.Partial, await _planner.DetectAsync(tweak, _context, CancellationToken.None));
	}

	[Fact]
	public async Task PlanAsync_ignores_duplicate_selections()
	{
		TweakDefinition tweakDefinition = Tweak("system.once");
		Assert.Single((await _planner.PlanAsync(new global::_003C_003Ez__ReadOnlyArray<TweakDefinition>(new TweakDefinition[2] { tweakDefinition, tweakDefinition }), Desktop, _context, CancellationToken.None)).Tweaks);
	}

	[Fact]
	public async Task PlanAsync_rejects_dependency_cycles()
	{
		TweakDefinition first = Tweak("system.first", null, new global::_003C_003Ez__ReadOnlySingleElementList<string>("system.second"));
		TweakDefinition second = Tweak("system.second", null, new global::_003C_003Ez__ReadOnlySingleElementList<string>("system.first"));
		Assert.Contains("cycle", (await Assert.ThrowsAsync<InvalidOperationException>(() => _planner.PlanAsync(new global::_003C_003Ez__ReadOnlyArray<TweakDefinition>(new TweakDefinition[2] { first, second }), Desktop, _context, CancellationToken.None))).Message, StringComparison.OrdinalIgnoreCase);
	}

	private static TweakDefinition Tweak(string id, Requirements? requirements = null, IReadOnlyList<string>? dependsOn = null, IReadOnlyList<string>? conflictsWith = null, IReadOnlyList<ConditionSpec>? detect = null)
	{
		return new TweakDefinition
		{
			Id = id,
			Name = id,
			Category = TweakCategory.System,
			Tier = TweakTier.Verified,
			Description = "Test tweak",
			Source = "https://example.test/source",
			Requires = (requirements ?? Requirements.None),
			DependsOn = (dependsOn ?? Array.Empty<string>()),
			ConflictsWith = (conflictsWith ?? Array.Empty<string>()),
			Detect = (detect ?? Array.Empty<ConditionSpec>()),
			Apply = new global::_003C_003Ez__ReadOnlySingleElementList<OperationSpec>(new OperationSpec
			{
				Type = "noop"
			}),
			Revert = new global::_003C_003Ez__ReadOnlySingleElementList<OperationSpec>(new OperationSpec
			{
				Type = "noop"
			})
		};
	}

	private static ConditionSpec Condition(bool matches)
	{
		return new ConditionSpec
		{
			Type = "boolean",
			Payload = new Dictionary<string, JsonElement> { ["matches"] = JsonSerializer.SerializeToElement(matches) }
		};
	}
}

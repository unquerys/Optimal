using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Optimal.Core.Detection;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;

namespace Optimal.Core.Execution;

public sealed class ExecutionPlanner
{
	private readonly OperationRegistry _registry;

	private readonly ILogger<ExecutionPlanner> _logger;

	public ExecutionPlanner(OperationRegistry registry, ILogger<ExecutionPlanner> logger)
	{
		_registry = registry;
		_logger = logger;
	}

	public async Task<ExecutionPlan> PlanAsync(IReadOnlyList<TweakDefinition> selection, MachineProfile profile, OperationContext context, CancellationToken cancellationToken)
	{
		List<ExcludedTweak> excluded = new List<ExcludedTweak>();
		List<TweakDefinition> list = new List<TweakDefinition>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (TweakDefinition item in selection)
		{
			if (!hashSet.Add(item.Id))
			{
				_logger.LogDebug("Ignoring duplicate selection of {TweakId}.", item.Id);
				continue;
			}
			ExcludedTweak excludedTweak = Evaluate(item, profile);
			if ((object)excludedTweak != null)
			{
				excluded.Add(excludedTweak);
			}
			else
			{
				list.Add(item);
			}
		}
		list = RemoveUnavailableDependencies(list, hashSet, excluded);
		list = RemoveConflicts(list, excluded);
		List<TweakDefinition> list2 = TopologicallySort(list);
		List<PlannedTweak> planned = new List<PlannedTweak>(list2.Count);
		foreach (TweakDefinition tweak in list2)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TweakState currentState = await DetectAsync(tweak, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			IReadOnlyList<string> descriptions = Describe(tweak.Apply);
			planned.Add(new PlannedTweak
			{
				Tweak = tweak,
				CurrentState = currentState,
				Descriptions = descriptions
			});
		}
		_logger.LogInformation("Planned {Planned} tweaks ({Changes} will change something), excluded {Excluded}.", planned.Count, planned.Count((PlannedTweak p) => !p.AlreadyApplied), excluded.Count);
		return new ExecutionPlan
		{
			Tweaks = planned,
			Excluded = excluded
		};
	}

	public async Task<TweakState> DetectAsync(TweakDefinition tweak, OperationContext context, CancellationToken cancellationToken)
	{
		if (tweak.Detect.Count == 0)
		{
			return TweakState.Unknown;
		}
		int matched = 0;
		foreach (ConditionSpec item in tweak.Detect)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				if (await _registry.GetCondition(item.Type).EvaluateAsync(item, context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
				{
					matched++;
				}
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Detection failed for tweak {TweakId}.", tweak.Id);
				return TweakState.Unknown;
			}
		}
		if (matched == tweak.Detect.Count)
		{
			return TweakState.Applied;
		}
		return (matched == 0) ? TweakState.NotApplied : TweakState.Partial;
	}

	public IReadOnlyList<string> Describe(IReadOnlyList<OperationSpec> operations)
	{
		List<string> list = new List<string>(operations.Count);
		foreach (OperationSpec operation in operations)
		{
			try
			{
				list.Add(_registry.GetOperation(operation.Type).Describe(operation));
			}
			catch (Exception exception)
			{
				_logger.LogWarning(exception, "Could not describe a '{Type}' operation.", operation.Type);
				list.Add(operation.Type + " operation");
			}
		}
		return list;
	}

	private static ExcludedTweak? Evaluate(TweakDefinition tweak, MachineProfile profile)
	{
		Requirements requires = tweak.Requires;
		int? minBuild = requires.MinBuild;
		if (minBuild.HasValue)
		{
			int valueOrDefault = minBuild.GetValueOrDefault();
			if (profile.Build < valueOrDefault)
			{
				return Exclude(tweak, ExclusionReason.UnsupportedBuild, $"Needs Windows build {valueOrDefault} or later. This machine is on {profile.Build}.");
			}
		}
		minBuild = requires.MaxBuild;
		if (minBuild.HasValue)
		{
			int valueOrDefault2 = minBuild.GetValueOrDefault();
			if (profile.Build > valueOrDefault2)
			{
				return Exclude(tweak, ExclusionReason.UnsupportedBuild, $"Only applies to Windows build {valueOrDefault2} or earlier. This machine is on {profile.Build}.");
			}
		}
		IReadOnlyList<string> editions = requires.Editions;
		if (editions != null && editions.Count > 0 && !editions.Contains<string>(profile.Edition, StringComparer.OrdinalIgnoreCase))
		{
			return Exclude(tweak, ExclusionReason.UnsupportedEdition, $"Only applies to {string.Join(" or ", editions)}. This machine runs {profile.Edition}.");
		}
		if (requires.DeviceKind != DeviceKind.Any && requires.DeviceKind != profile.DeviceKind)
		{
			return Exclude(tweak, ExclusionReason.WrongDeviceKind, $"Only applies to a {requires.DeviceKind.ToString().ToLowerInvariant()}. This machine is a {profile.DeviceKind.ToString().ToLowerInvariant()}.");
		}
		if (requires.GpuVendor != GpuVendor.Any && requires.GpuVendor != profile.GpuVendor)
		{
			return Exclude(tweak, ExclusionReason.WrongGpuVendor, $"Needs an {requires.GpuVendor} GPU. This machine has {profile.GpuName}.");
		}
		IReadOnlyList<string> capabilities = requires.Capabilities;
		if (capabilities != null && capabilities.Count > 0)
		{
			List<string> list = capabilities.Where((string c) => !profile.HasCapability(c)).ToList();
			if (list.Count > 0)
			{
				return Exclude(tweak, ExclusionReason.MissingCapability, "This machine does not report support for: " + string.Join(", ", list) + ".");
			}
		}
		return null;
	}

	private static ExcludedTweak Exclude(TweakDefinition tweak, ExclusionReason reason, string explanation)
	{
		return new ExcludedTweak
		{
			Tweak = tweak,
			Reason = reason,
			Explanation = explanation
		};
	}

	private List<TweakDefinition> RemoveUnavailableDependencies(List<TweakDefinition> eligible, IReadOnlySet<string> selectedIds, List<ExcludedTweak> excluded)
	{
		Dictionary<string, TweakDefinition> remaining = eligible.ToDictionary<TweakDefinition, string>((TweakDefinition t) => t.Id, StringComparer.OrdinalIgnoreCase);
		bool flag = true;
		while (flag)
		{
			flag = false;
			foreach (TweakDefinition item in remaining.Values.ToList())
			{
				List<string> list = item.DependsOn.Where((string id) => !selectedIds.Contains(id) || !remaining.ContainsKey(id)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
				if (list.Count != 0)
				{
					remaining.Remove(item.Id);
					excluded.Add(Exclude(item, ExclusionReason.MissingDependency, "Requires " + string.Join(", ", list) + ", which is not available in this plan."));
					_logger.LogWarning("Dropped {TweakId} because dependencies are unavailable: {Dependencies}.", item.Id, string.Join(", ", list));
					flag = true;
				}
			}
		}
		return eligible.Where((TweakDefinition t) => remaining.ContainsKey(t.Id)).ToList();
	}

	private List<TweakDefinition> RemoveConflicts(List<TweakDefinition> eligible, List<ExcludedTweak> excluded)
	{
		Dictionary<string, TweakDefinition> dictionary = eligible.ToDictionary<TweakDefinition, string>((TweakDefinition t) => t.Id, StringComparer.OrdinalIgnoreCase);
		HashSet<string> conflicted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (TweakDefinition item in eligible)
		{
			foreach (string item2 in item.ConflictsWith)
			{
				if (dictionary.ContainsKey(item2))
				{
					conflicted.Add(item.Id);
					conflicted.Add(item2);
				}
			}
		}
		if (conflicted.Count == 0)
		{
			return eligible;
		}
		foreach (string id in conflicted)
		{
			TweakDefinition tweakDefinition = dictionary[id];
			IEnumerable<string> values = from other in tweakDefinition.ConflictsWith.Where(dictionary.ContainsKey).Concat(from t in eligible
					where t.ConflictsWith.Contains<string>(id, StringComparer.OrdinalIgnoreCase)
					select t.Id).Distinct<string>(StringComparer.OrdinalIgnoreCase)
				where !string.Equals(other, id, StringComparison.OrdinalIgnoreCase)
				select other;
			excluded.Add(Exclude(tweakDefinition, ExclusionReason.ConflictsWithSelection, "Conflicts with " + string.Join(", ", values) + ", which is also selected. Choose one of them."));
			_logger.LogWarning("Dropped {TweakId} because of a conflict inside the selection.", id);
		}
		return eligible.Where((TweakDefinition t) => !conflicted.Contains(t.Id)).ToList();
	}

	internal static List<TweakDefinition> TopologicallySort(IReadOnlyList<TweakDefinition> tweaks)
	{
		Dictionary<string, TweakDefinition> byId = tweaks.ToDictionary<TweakDefinition, string>((TweakDefinition t) => t.Id, StringComparer.OrdinalIgnoreCase);
		List<TweakDefinition> sorted = new List<TweakDefinition>(tweaks.Count);
		Dictionary<string, int> state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Stack<string> path = new Stack<string>();
		foreach (TweakDefinition tweak in tweaks)
		{
			Visit(tweak);
		}
		return sorted;
		void Visit(TweakDefinition tweak)
		{
			switch (state.GetValueOrDefault(tweak.Id, 0))
			{
			case 2:
				break;
			case 1:
			{
				string text = string.Join(" -> ", path.Reverse().Append(tweak.Id));
				throw new InvalidOperationException("Tweak dependencies form a cycle: " + text);
			}
			default:
				state[tweak.Id] = 1;
				path.Push(tweak.Id);
				foreach (string item in tweak.DependsOn)
				{
					if (byId.TryGetValue(item, out var value))
					{
						Visit(value);
					}
				}
				path.Pop();
				state[tweak.Id] = 2;
				sorted.Add(tweak);
				break;
			}
		}
	}
}

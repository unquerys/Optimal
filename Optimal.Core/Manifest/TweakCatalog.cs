using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimal.Core.Manifest;

public sealed class TweakCatalog
{
	private readonly Dictionary<string, TweakDefinition> _byId;

	public IReadOnlyList<TweakDefinition> Tweaks { get; }

	public int Count => _byId.Count;

	public TweakDefinition this[string id] => _byId[id];

	public TweakCatalog(IReadOnlyDictionary<string, TweakDefinition> byId)
	{
		_byId = new Dictionary<string, TweakDefinition>(byId, StringComparer.OrdinalIgnoreCase);
		Tweaks = _byId.Values.OrderBy((TweakDefinition t) => t.Category).ThenBy<TweakDefinition, string>((TweakDefinition t) => t.Id, StringComparer.Ordinal).ToList();
	}

	public bool Contains(string id)
	{
		return _byId.ContainsKey(id);
	}

	public bool TryGet(string id, out TweakDefinition tweak)
	{
		return _byId.TryGetValue(id, out tweak);
	}

	public bool TryResolve(IEnumerable<string> ids, out IReadOnlyList<TweakDefinition> resolved, out IReadOnlyList<string> unknown)
	{
		List<TweakDefinition> list = new List<TweakDefinition>();
		List<string> list2 = new List<string>();
		foreach (string id in ids)
		{
			if (_byId.TryGetValue(id, out TweakDefinition value))
			{
				list.Add(value);
			}
			else
			{
				list2.Add(id);
			}
		}
		resolved = list;
		unknown = list2;
		return list2.Count == 0;
	}

	public IEnumerable<TweakDefinition> ByCategory(TweakCategory category)
	{
		return Tweaks.Where((TweakDefinition t) => t.Category == category);
	}

	public IEnumerable<TweakDefinition> ByTier(TweakTier tier)
	{
		return Tweaks.Where((TweakDefinition t) => t.Tier == tier);
	}
}

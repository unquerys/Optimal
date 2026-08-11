using System;
using System.Collections.Generic;
using System.Linq;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed class OperationRegistry
{
	private readonly Dictionary<string, IOperationHandler> _operations;

	private readonly Dictionary<string, IConditionHandler> _conditions;

	private readonly IReadOnlyList<IBackupRestorer> _restorers;

	public IReadOnlyCollection<string> OperationTypes => _operations.Keys;

	public IReadOnlyCollection<string> ConditionTypes => _conditions.Keys;

	public OperationRegistry(IEnumerable<IOperationHandler> operations, IEnumerable<IConditionHandler> conditions)
	{
		_operations = operations.ToDictionary<IOperationHandler, string>((IOperationHandler h) => h.Type, StringComparer.OrdinalIgnoreCase);
		_conditions = conditions.ToDictionary<IConditionHandler, string>((IConditionHandler h) => h.Type, StringComparer.OrdinalIgnoreCase);
		_restorers = _operations.Values.OfType<IBackupRestorer>().Concat(_conditions.Values.OfType<IBackupRestorer>()).Distinct()
			.ToList();
	}

	public IOperationHandler GetOperation(string type)
	{
		if (!_operations.TryGetValue(type, out IOperationHandler value))
		{
			throw new ManifestValidationException($"No handler is registered for operation type '{type}'. Known types: {string.Join(", ", _operations.Keys.Order())}.");
		}
		return value;
	}

	public IConditionHandler GetCondition(string type)
	{
		if (!_conditions.TryGetValue(type, out IConditionHandler value))
		{
			throw new ManifestValidationException($"No handler is registered for condition type '{type}'. Known types: {string.Join(", ", _conditions.Keys.Order())}.");
		}
		return value;
	}

	public IBackupRestorer GetRestorer(BackupEntry entry)
	{
		return _restorers.FirstOrDefault((IBackupRestorer r) => r.CanRestore(entry)) ?? throw new InvalidOperationException("No restorer can handle a backup entry of type " + entry.GetType().Name + ".");
	}

	public static OperationRegistry CreateDefault(IProcessRunner processRunner)
	{
		PowerCfg powerCfg = new PowerCfg(processRunner);
		WingetHandler item = new WingetHandler(processRunner);
		AppxHandler item2 = new AppxHandler(processRunner);
		List<IOperationHandler> operations = new List<IOperationHandler>
		{
			new RegistryOperationHandler(),
			new PowerCfgOperationHandler(powerCfg),
			item,
			item2,
			new NvidiaProfileHandler(processRunner)
		};
		List<IConditionHandler> conditions = new List<IConditionHandler>
		{
			new RegistryConditionHandler(),
			item,
			item2
		};
		return new OperationRegistry(operations, conditions);
	}
}

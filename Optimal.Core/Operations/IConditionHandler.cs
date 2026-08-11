using System.Threading;
using System.Threading.Tasks;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public interface IConditionHandler
{
	string Type { get; }

	void Validate(ConditionSpec spec);

	string Describe(ConditionSpec spec);

	Task<bool> EvaluateAsync(ConditionSpec spec, OperationContext context, CancellationToken cancellationToken);
}

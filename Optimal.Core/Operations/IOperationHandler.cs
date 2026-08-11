using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public interface IOperationHandler
{
	string Type { get; }

	void Validate(OperationSpec spec);

	string Describe(OperationSpec spec);

	Task<IReadOnlyList<BackupEntry>> CaptureAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken);

	Task ExecuteAsync(OperationSpec spec, OperationContext context, CancellationToken cancellationToken);
}

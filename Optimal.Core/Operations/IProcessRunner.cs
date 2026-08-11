using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Optimal.Core.Operations;

public interface IProcessRunner
{
	Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

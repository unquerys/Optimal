using System.Threading;
using System.Threading.Tasks;

namespace Optimal.Core.Operations;

public interface IBackupRestorer
{
	bool CanRestore(BackupEntry entry);

	Task RestoreAsync(BackupEntry entry, OperationContext context, CancellationToken cancellationToken);
}

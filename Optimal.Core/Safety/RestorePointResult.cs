namespace Optimal.Core.Safety;

public sealed record RestorePointResult(RestorePointStatus Status, string Message, long? SequenceNumber = null)
{
	public bool Created => Status == RestorePointStatus.Created;
}

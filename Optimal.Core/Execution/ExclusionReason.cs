namespace Optimal.Core.Execution;

public enum ExclusionReason
{
	None,
	UnsupportedBuild,
	UnsupportedEdition,
	WrongDeviceKind,
	WrongGpuVendor,
	MissingCapability,
	MissingDependency,
	ConflictsWithSelection
}

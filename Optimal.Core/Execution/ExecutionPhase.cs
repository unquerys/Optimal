namespace Optimal.Core.Execution;

public enum ExecutionPhase
{
	CreatingRestorePoint,
	BackingUpRegistry,
	ApplyingTweaks,
	Finishing
}

namespace Optimal.Core.Execution;

public sealed record RunOptions
{
	public bool DryRun { get; init; }

	public bool BackupRegistry { get; init; } = true;

	public string? PresetName { get; init; }
}

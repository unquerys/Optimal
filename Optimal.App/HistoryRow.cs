using Optimal.Core.Safety;

namespace Optimal.App;

public sealed record HistoryRow(RunRecord Record)
{
	public string Id => Record.RunId;

	public string Title => Record.PresetName ?? Record.Mode.ToString();

	public string Date => Record.StartedUtc.ToLocalTime().ToString("MMM d, yyyy · h:mm tt");

	public string Summary => $"{Record.AppliedCount} applied  ·  {Record.SkippedCount} skipped  ·  {Record.FailedCount} failed";

	public bool CanRevert => Record.CanRevert;

	public string Mode => Record.Mode.ToString().ToUpperInvariant();
}

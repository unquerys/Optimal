using System.Collections.Generic;
using System.Windows.Media;

namespace Optimal.App;

public sealed class GameProfile
{
	public required string Title { get; init; }
	public required string ShortName { get; init; }
	public required string Genre { get; init; }
	public required string Pace { get; init; }
	public required string Description { get; init; }
	public required string Target { get; init; }
	public required string Preset { get; init; }
	public required string CoverPath { get; init; }
	public required Brush CoverBrush { get; init; }
	public required IReadOnlyList<GameSettingRow> Settings { get; init; }
	public required IReadOnlyList<string> TweakIds { get; init; }

	public string SearchText => $"{Title} {Genre} {Pace}";
}

public sealed class GameSettingRow
{
	public required string Name { get; init; }
	public required string Value { get; init; }
	public required string Reason { get; init; }
}

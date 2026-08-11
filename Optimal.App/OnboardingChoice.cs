namespace Optimal.App;

public sealed class OnboardingChoice
{
	public string Id { get; }

	public string Name { get; }

	public string Detail { get; }

	public string Glyph { get; }

	public string? LogoPath { get; }

	public bool IsSelected { get; set; }

	public OnboardingChoice(string id, string name, string detail, string glyph, string? logoPath)
	{
		Id = id;
		Name = name;
		Detail = detail;
		Glyph = glyph;
		LogoPath = logoPath;
	}
}

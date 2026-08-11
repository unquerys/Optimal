namespace Optimal.Core.Operations;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
	public bool Succeeded => ExitCode == 0;

	public string CombinedOutput
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(StandardError))
			{
				return (StandardOutput.Trim() + "\n" + StandardError.Trim()).Trim();
			}
			return StandardOutput.Trim();
		}
	}
}

namespace Optimal.Core.Detection;

public sealed record ProbeProgress(string Stage, int Completed, int Total)
{
	public double Fraction
	{
		get
		{
			if (Total != 0)
			{
				return (double)Completed / (double)Total;
			}
			return 0.0;
		}
	}
}

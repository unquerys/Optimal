namespace Optimal.Core.Detection;

public sealed record HardwareRecommendation(string ProfileId, string Name, string Confidence, string Rationale, bool CanApplyAutomatically);

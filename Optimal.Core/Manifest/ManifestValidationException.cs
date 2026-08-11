using System;

namespace Optimal.Core.Manifest;

public sealed class ManifestValidationException : Exception
{
	public string? SourceFile { get; init; }

	public string? TweakId { get; init; }

	public ManifestValidationException(string message)
		: base(message)
	{
	}

	public ManifestValidationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	public override string ToString()
	{
		string? sourceFile = SourceFile;
		string tweakId = TweakId;
		string text = ((sourceFile == null) ? ((tweakId == null) ? string.Empty : (" [" + TweakId + "]")) : ((tweakId == null) ? (" [" + SourceFile + "]") : $" [{SourceFile} :: {TweakId}]"));
		string text2 = text;
		return "ManifestValidationException" + text2 + ": " + Message;
	}
}

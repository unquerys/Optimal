using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Xunit;

namespace Optimal.Tests;

public sealed class ManifestAudienceTests
{
	private static ManifestLoader Loader()
	{
		return new ManifestLoader(OperationRegistry.CreateDefault(new ProcessRunner()), NullLogger<ManifestLoader>.Instance);
	}

	[Fact]
	public void LoadFromJson_defaults_omitted_audience_to_simple()
	{
		TweakDefinition tweakDefinition = Loader().LoadFromJson(Manifest()).Single();
		Assert.Equal(TweakAudience.Simple, tweakDefinition.Audience);
	}

	[Fact]
	public void LoadFromJson_reads_advanced_audience()
	{
		TweakDefinition tweakDefinition = Loader().LoadFromJson(Manifest("\"audience\": \"advanced\",")).Single();
		Assert.Equal(TweakAudience.Advanced, tweakDefinition.Audience);
	}

	private static string Manifest(string audience = "")
	{
		return "{\n  \"schemaVersion\": 1,\n  \"tweaks\": [{\n    \"id\": \"system.test.control\",\n    \"name\": \"Test control\",\n    \"category\": \"system\",\n    \"tier\": \"verified\",\n    " + audience + "\n    \"description\": \"A reversible test control.\",\n    \"source\": \"https://learn.microsoft.com/windows/\",\n    \"apply\": [{ \"type\": \"registry\", \"action\": \"set\", \"path\": \"HKCU\\\\Software\\\\OptimalTest\", \"name\": \"Value\", \"valueType\": \"dword\", \"value\": 1 }],\n    \"revert\": [{ \"type\": \"registry\", \"action\": \"delete\", \"path\": \"HKCU\\\\Software\\\\OptimalTest\", \"name\": \"Value\" }]\n  }]\n}";
	}
}

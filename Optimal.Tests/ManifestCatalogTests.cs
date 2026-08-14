using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Xunit;

namespace Optimal.Tests;

public sealed class ManifestCatalogTests
{
	[Fact]
	public async Task Repository_catalog_is_valid_and_contains_expanded_controls()
	{
		string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "manifests"));
		ManifestLoader loader = new(
			OperationRegistry.CreateDefault(new ProcessRunner()),
			NullLogger<ManifestLoader>.Instance);

		TweakCatalog catalog = await loader.LoadDirectoryAsync(directory, CancellationToken.None);

		Assert.True(catalog.Tweaks.Count >= 90);
		Assert.True(catalog.TryGet("debloat.outlook-new.remove", out _));
		Assert.True(catalog.TryGet("network.llmnr.disable", out _));
	}
}

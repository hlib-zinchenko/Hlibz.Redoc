using System.Text;

using Xunit;

namespace Hlibz.Redoc.Extensions.Tests;

/// <summary>
/// The embedded ReDoc bundle: that it is there, that it is the version this package claims, and
/// that it survives the round trip out of the assembly.
/// </summary>
/// <remarks>
/// Worth asserting because the failure mode is quiet: an <c>EmbeddedResource</c> that stops
/// matching its file, or a bundle upgraded without updating <see cref="ReDocBundle.Version"/>,
/// produces a package that looks fine until someone opens a page.
/// </remarks>
public class ReDocBundleTests
{
    [Fact]
    public void Gzip_IsEmbedded()
    {
        Assert.True(ReDocBundle.Gzip.Length > 100_000);
    }

    [Fact]
    public void Raw_IsTheVersionTheConstantClaims()
    {
        string bundle = Encoding.UTF8.GetString(ReDocBundle.Raw());

        // ReDoc prints its own version to the console on startup, so the bundle carries the
        // string: the one place the file itself can be asked what it is.
        Assert.Contains(ReDocBundle.Version, bundle, StringComparison.Ordinal);
    }

    [Fact]
    public void Raw_IsTheWholeBundle()
    {
        byte[] raw = ReDocBundle.Raw();

        Assert.True(raw.Length > ReDocBundle.Gzip.Length * 2);
        Assert.Contains(
            "Redocly", Encoding.UTF8.GetString(raw, 0, 2000), StringComparison.Ordinal);
    }

    [Fact]
    public void ETag_IsStrongAndQuoted()
    {
        Assert.StartsWith("\"", ReDocBundle.ETag, StringComparison.Ordinal);
        Assert.EndsWith("\"", ReDocBundle.ETag, StringComparison.Ordinal);
    }
}

using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Infrastructure;

public class FileHasherTests
{
    [Fact]
    public void ComputeHash_SameContent_SameHash()
    {
        var hash1 = FileHasher.ComputeHash("hello world");
        var hash2 = FileHasher.ComputeHash("hello world");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var hash1 = FileHasher.ComputeHash("hello");
        var hash2 = FileHasher.ComputeHash("world");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsHexString()
    {
        var hash = FileHasher.ComputeHash("test");
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_EmptyString_ReturnsValidHash()
    {
        var hash = FileHasher.ComputeHash("");
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
    }
}

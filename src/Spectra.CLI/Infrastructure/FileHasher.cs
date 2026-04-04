using System.Security.Cryptography;
using System.Text;

namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Computes SHA-256 hashes for file content comparison.
/// </summary>
public static class FileHasher
{
    /// <summary>
    /// Computes SHA-256 hash of a string.
    /// </summary>
    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA-256 hash of a file's content.
    /// </summary>
    public static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        return ComputeHash(content);
    }
}

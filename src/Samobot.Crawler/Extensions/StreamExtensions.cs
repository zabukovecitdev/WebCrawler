using System.Security.Cryptography;

namespace Samobot.Crawler.Extensions;

public static class StreamExtensions
{
    public static async Task<string> ToHash(this Stream stream)
    {
        stream.Position = 0;
        
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
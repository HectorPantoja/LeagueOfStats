using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LeagueOfStats.Application;
namespace LeagueOfStats.Infrastructure.Riot;

// This instance owns its HTTP client. Only official, versioned square PNG assets are accepted.
public sealed class DataDragonImageCache(HttpClient http, string directory, IAppDiagnostics diagnostics,
    TimeProvider? timeProvider = null) : IImageAssetCache, IDisposable
{
    private const int MaximumBytes = 1024 * 1024;
    private readonly string root = Path.GetFullPath(directory);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim downloads = new(3, 3);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> retryAfter = new(StringComparer.Ordinal);
    private static readonly Regex AssetPath = new(@"\A/cdn/(?<version>[0-9]+\.[0-9]+\.[0-9]+)/img/(?<kind>champion|item)/(?<file>[A-Za-z0-9_-]+\.png)\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public async Task<string?> GetAsync(string officialUrl, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(officialUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "ddragon.leagueoflegends.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0) return null;
        var match = AssetPath.Match(uri.AbsolutePath);
        if (!match.Success) return null;
        var path = Path.Combine(root, match.Groups["version"].Value, match.Groups["kind"].Value, match.Groups["file"].Value);
        var gate = gates.GetOrAdd(uri.AbsoluteUri, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        string? temporary = null;
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Length <= MaximumBytes && ValidPng(await File.ReadAllBytesAsync(path, token)))
                    return new Uri(path).AbsoluteUri;
                File.Delete(path); // Only the validated asset path in this cache.
            }
            if (retryAfter.TryGetValue(uri.AbsoluteUri, out var next) && next > clock.GetUtcNow()) return null;
            await downloads.WaitAsync(token);
            byte[] bytes;
            try
            {
                using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
                request.CancelAfter(TimeSpan.FromSeconds(10));
                using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, request.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidDataException("Image is too large.");
                await using var stream = await response.Content.ReadAsStreamAsync(request.Token);
                using var buffer = new MemoryStream();
                var block = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(block, request.Token)) > 0)
                {
                    if (buffer.Length + read > MaximumBytes) throw new InvalidDataException("Image is too large.");
                    buffer.Write(block, 0, read);
                }
                bytes = buffer.ToArray();
                if (!ValidPng(bytes)) throw new InvalidDataException("Invalid square PNG asset.");
            }
            finally { downloads.Release(); }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            temporary = null;
            retryAfter.TryRemove(uri.AbsoluteUri, out _);
            diagnostics.Write("asset.cached");
            return new Uri(path).AbsoluteUri;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException)
        {
            retryAfter[uri.AbsoluteUri] = clock.GetUtcNow().AddMinutes(1);
            diagnostics.Write("asset.download.failed", error: ex);
            return null;
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            gate.Release();
        }
    }

    private static bool ValidPng(byte[] bytes)
    {
        if (bytes.Length < 45 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) || !bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8)) return false;
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        return width is > 0 and <= 1024 && height is > 0 and <= 1024;
    }

    public void Dispose()
    {
        http.Dispose();
        downloads.Dispose();
        foreach (var gate in gates.Values) gate.Dispose();
    }
}

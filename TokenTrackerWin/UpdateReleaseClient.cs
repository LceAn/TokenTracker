using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace TokenTrackerWin;

/// <summary>
/// Resolves a release without downloading or executing an installer. A throttled
/// REST API falls back to the release redirect, with both assets pinned to that
/// tag. Kept UI-free so the HTTP flow can be tested without an app install.
/// Releases are looked up per <see cref="UpdateChannel"/>; the channel-less
/// entry point keeps resolving the official repository.
/// </summary>
internal sealed class UpdateReleaseClient(HttpClient http, Func<DateTimeOffset>? utcNow = null)
{
    internal const string ReleasePageUrl = "https://github.com/xiufengsun/TokenTracker/releases/latest";
    internal const string SetupAssetName = "TokenTracker-Setup.exe";
    private readonly Func<DateTimeOffset> _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    // One cooldown for both channels: the unauthenticated API limit is per client IP.
    private DateTimeOffset? _apiNotBefore;

    /// <param name="Version">Version to show the user: "1.5.28", or "1.5.28-fork.1" for a fork release.</param>
    /// <param name="Channel">The channel the release was found in.</param>
    /// <param name="BaseVersion">Upstream version the release is based on.</param>
    /// <param name="ForkRevision">Fork revision, for a fork release.</param>
    internal readonly record struct ReleaseInfo(
        string Version, string SetupUrl, long SetupSize, string? ChecksumsUrl,
        UpdateChannel Channel, string BaseVersion, int? ForkRevision);

    public DateTimeOffset? ApiRetryAt { get; private set; }
    public bool UsedFallback { get; private set; }

    /// <summary>Latest official release.</summary>
    public Task<ReleaseInfo> FetchLatestAsync(CancellationToken cancellationToken = default)
        => FetchLatestAsync(UpdateChannel.Official, cancellationToken);

    /// <summary>Latest release of <paramref name="channel"/>.</summary>
    public async Task<ReleaseInfo> FetchLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        UsedFallback = false;
        if (_apiNotBefore is not { } retryAt || retryAt <= _utcNow())
        {
            ApiRetryAt = null;
            using var request = new HttpRequestMessage(HttpMethod.Get, channel.ApiUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Diag.Log("update", $"github api status {(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
                return await ReadApiReleaseAsync(response, channel, cancellationToken);

            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
                response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var exhausted = Header(response, "x-ratelimit-remaining") == "0";
            if (response.StatusCode != HttpStatusCode.TooManyRequests && !exhausted
                && response.Headers.RetryAfter is null
                && !body.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                response.EnsureSuccessStatusCode();

            ApiRetryAt = ResolveRetryAt(response, exhausted);
            _apiNotBefore = ApiRetryAt ?? _utcNow().AddMinutes(1);
            Diag.Log("update", $"github api rate limited; retry_at={_apiNotBefore:O}");
        }

        UsedFallback = true;
        Diag.Log("update", $"checking {channel.Id} release page (api cooldown)");
        return await ReadReleasePageAsync(channel, cancellationToken);
    }

    private static async Task<ReleaseInfo> ReadApiReleaseAsync(
        HttpResponseMessage response, UpdateChannel channel, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var (baseVersion, forkRevision) = ParseTag(tag, channel);
        string? setupUrl = null;
        long setupSize = 0;
        string? checksumsUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!asset.TryGetProperty("browser_download_url", out var u)) continue;
                if (name.Equals(SetupAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    setupUrl = u.GetString();
                    setupSize = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0;
                }
                else if (name.Equals(UpdateIntegrity.ChecksumsAssetName, StringComparison.OrdinalIgnoreCase))
                    checksumsUrl = u.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(setupUrl) || setupSize <= 0)
            throw new InvalidDataException("The release has no usable Windows installer asset.");
        return Release(channel, baseVersion, forkRevision, setupUrl, setupSize, checksumsUrl);
    }

    private async Task<ReleaseInfo> ReadReleasePageAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, channel.ReleasePageUrl);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        Diag.Log("update", $"release page status {(int)response.StatusCode}");
        response.EnsureSuccessStatusCode();
        var uri = response.RequestMessage?.RequestUri;
        if (uri is null || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !uri.AbsolutePath.StartsWith(channel.TagPath, StringComparison.Ordinal))
            throw new InvalidDataException("The release page did not resolve to a release tag of this update channel.");

        var tag = uri.AbsolutePath[channel.TagPath.Length..];
        var (baseVersion, forkRevision) = ParseTag(tag, channel);
        var setupUrl = channel.DownloadBase + tag + "/" + SetupAssetName;
        var checksumsUrl = channel.DownloadBase + tag + "/" + UpdateIntegrity.ChecksumsAssetName;
        using var setupRequest = new HttpRequestMessage(HttpMethod.Head, setupUrl);
        using var setupResponse = await http.SendAsync(setupRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        Diag.Log("update", $"release installer metadata status {(int)setupResponse.StatusCode}");
        setupResponse.EnsureSuccessStatusCode();
        var setupSize = setupResponse.Content.Headers.ContentLength ?? 0;
        if (setupSize <= 0)
            throw new InvalidDataException("The release installer size is unavailable.");

        using var checksumsResponse = await http.GetAsync(checksumsUrl, cancellationToken);
        Diag.Log("update", $"release checksums status {(int)checksumsResponse.StatusCode}");
        checksumsResponse.EnsureSuccessStatusCode();
        var checksums = await checksumsResponse.Content.ReadAsStringAsync(cancellationToken);
        if (UpdateIntegrity.FindDigest(checksums, SetupAssetName) is null)
            throw new InvalidDataException("The release has no usable Windows installer checksum.");

        Diag.Log("update", $"{channel.Id} release page resolved version={Display(baseVersion, forkRevision)}");
        return Release(channel, baseVersion, forkRevision, setupUrl, setupSize, checksumsUrl);
    }

    private static ReleaseInfo Release(
        UpdateChannel channel, string baseVersion, int? forkRevision, string setupUrl, long setupSize, string? checksumsUrl)
        => new(Display(baseVersion, forkRevision), setupUrl, setupSize, checksumsUrl, channel, baseVersion, forkRevision);

    private static string Display(string baseVersion, int? forkRevision)
        => forkRevision is { } revision ? $"{baseVersion}-fork.{revision}" : baseVersion;

    private DateTimeOffset? ResolveRetryAt(HttpResponseMessage response, bool exhausted)
    {
        var now = _utcNow();
        DateTimeOffset? retryAt = response.Headers.RetryAfter?.Date;
        if (response.Headers.RetryAfter?.Delta is { } delta)
            retryAt = now + delta;
        if (exhausted && long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.None,
                CultureInfo.InvariantCulture, out var epoch)
            && epoch is >= 0 and <= 253402300799)
        {
            var resetAt = DateTimeOffset.FromUnixTimeSeconds(epoch);
            if (retryAt is null || resetAt > retryAt) retryAt = resetAt;
        }
        return retryAt is { } candidate && candidate > now ? candidate : null;
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// Official releases are tagged <c>vX.Y.Z</c>. Fork releases carry a
    /// <c>-fork.N</c> suffix, and the fork channel refuses tags without one so a
    /// release can always be told apart from an official one.
    /// </summary>
    private static (string BaseVersion, int? ForkRevision) ParseTag(string tag, UpdateChannel channel)
    {
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        int? forkRevision = null;
        if (channel == UpdateChannel.Fork)
        {
            const string suffix = "-fork.";
            var at = version.LastIndexOf(suffix, StringComparison.Ordinal);
            var digits = at < 0 ? "" : version[(at + suffix.Length)..];
            if (digits.Length is 0 or > 9 || digits[0] == '0' || !digits.All(char.IsAsciiDigit))
                throw new InvalidDataException("The release tag is not a stable fork version.");
            forkRevision = int.Parse(digits, CultureInfo.InvariantCulture);
            version = version[..at];
        }
        if (!Version.TryParse(version, out var parsed) || parsed.Build < 0 || parsed.Revision >= 0)
            throw new InvalidDataException("The release tag is not a stable three-part version.");
        return (version, forkRevision);
    }
}

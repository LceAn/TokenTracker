using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using TokenTrackerWin;
using Xunit;

namespace TokenTrackerWin.Tests;

public sealed class UpdateChannelTests
{
    private static readonly InstalledRelease ForkBuild = new(UpdateChannel.Fork, "1.5.28", 1);
    private static readonly InstalledRelease OfficialBuild = new(UpdateChannel.Official, "1.5.28", null);

    private const string ForkApi = "https://api.github.com/repos/LceAn/TokenTracker/releases/latest";
    private const string ForkPage = "https://github.com/LceAn/TokenTracker/releases/latest";
    private const string ForkTagPage = "https://github.com/LceAn/TokenTracker/releases/tag/v1.5.28-fork.2";
    private const string OfficialApi = "https://api.github.com/repos/xiufengsun/TokenTracker/releases/latest";
    private const string ForkDownloads = "https://github.com/LceAn/TokenTracker/releases/download/v1.5.28-fork.2/";
    private const string OfficialDownloads = "https://github.com/xiufengsun/TokenTracker/releases/download/v1.5.28/";

    // ── identity ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("fork", true, true)]
    [InlineData("official", true, false)]
    [InlineData("Fork", false, false)]
    [InlineData("", false, false)]
    [InlineData("bogus", false, false)]
    [InlineData(null, false, false)]
    public void PersistedIdsResolveToChannelsAndRejectEverythingElse(string? id, bool resolved, bool isFork)
    {
        Assert.Equal(resolved, UpdateChannel.TryFromId(id, out var channel));
        Assert.Equal(isFork ? UpdateChannel.Fork : UpdateChannel.Official, channel);
    }

    [Theory]
    [InlineData("1.5.28-fork.1+abc123", true, "1.5.28", 1, "1.5.28-fork.1")]
    [InlineData("1.5.28-fork.12", true, "1.5.28", 12, "1.5.28-fork.12")]
    [InlineData("1.5.28+abc123", false, "1.5.28", null, "1.5.28")]
    [InlineData("1.0.0", false, "1.0.0", null, "1.0.0")]
    public void InformationalVersionCarriesTheChannelAndRevision(
        string informationalVersion, bool isFork, string version, int? revision, string display)
    {
        var installed = InstalledRelease.FromInformationalVersion(informationalVersion, "9.9.9");

        Assert.Equal(isFork ? UpdateChannel.Fork : UpdateChannel.Official, installed.Channel);
        Assert.Equal(version, installed.Version);
        Assert.Equal(revision, installed.ForkRevision);
        Assert.Equal(display, installed.DisplayVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.5")]
    public void UnrecognizedInformationalVersionIsAnOfficialBuildOnTheFallbackVersion(string? informationalVersion)
    {
        var installed = InstalledRelease.FromInformationalVersion(informationalVersion, "9.9.9");

        Assert.Equal(UpdateChannel.Official, installed.Channel);
        Assert.Equal("9.9.9", installed.Version);
        Assert.Null(installed.ForkRevision);
    }

    // ── decision ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, "1.5.28", 2, true)]        // new fork revision
    [InlineData(true, "1.5.28", 1, false)]       // this very release
    [InlineData(true, "1.5.29", 1, true)]        // new upstream base
    [InlineData(true, "1.5.27", 9, false)]       // an older base is never an update
    [InlineData(false, "1.5.28", null, true)]    // other channel, same base
    [InlineData(false, "1.5.29", null, true)]    // other channel, newer base
    [InlineData(false, "1.5.27", null, false)]   // other channel, but older
    public void AForkBuildIsOfferedNewerReleasesAndTheChosenChannelSwitches(
        bool releaseIsFork, string version, int? revision, bool expected)
    {
        var channel = releaseIsFork ? UpdateChannel.Fork : UpdateChannel.Official;

        Assert.Equal(expected, UpdateDecision.IsUpdate(ForkBuild, channel, version, revision));
    }

    [Fact]
    public void AnOlderForkReleaseIsNotOfferedToANewerOfficialBuild()
    {
        var newerOfficial = new InstalledRelease(UpdateChannel.Official, "1.5.30", null);

        Assert.False(UpdateDecision.IsUpdate(newerOfficial, UpdateChannel.Fork, "1.5.28", 1));
        Assert.False(UpdateDecision.IsUpdate(newerOfficial, UpdateChannel.Official, "1.5.30", null));
        Assert.True(UpdateDecision.IsUpdate(newerOfficial, UpdateChannel.Official, "1.5.31", null));
    }

    // ── persisted choice ───────────────────────────────────────────────

    [Fact]
    public void StoredChoiceWinsOverTheBuildsOwnChannel()
    {
        var settings = JsonNode.Parse($"{{\"{UpdateChannelPolicy.ChannelKey}\":\"official\"}}")!.AsObject();

        Assert.Equal(UpdateChannel.Official, UpdateChannelPolicy.ResolveSelected(settings, UpdateChannel.Fork));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"UpdateChecker.updateChannel\":\"bogus\"}")]
    [InlineData("{\"UpdateChecker.updateChannel\":true}")]
    public void MissingOrDamagedChoiceFallsBackToTheBuildsOwnChannel(string json)
    {
        var settings = JsonNode.Parse(json)?.AsObject();

        Assert.Equal(UpdateChannel.Fork, UpdateChannelPolicy.ResolveSelected(settings, UpdateChannel.Fork));
        Assert.Equal(UpdateChannel.Official, UpdateChannelPolicy.ResolveSelected(settings, UpdateChannel.Official));
    }

    // ── release lookup ─────────────────────────────────────────────────

    [Fact]
    public async Task ForkChannelReadsTheForkRepositoryAndParsesTheForkTag()
    {
        var calls = new List<string>();
        using var http = Http(request =>
        {
            calls.Add(request.RequestUri!.AbsoluteUri);
            return ApiRelease("v1.5.28-fork.2", ForkDownloads);
        });
        var resolver = new UpdateReleaseClient(http);

        var release = await resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None);

        Assert.Equal([ForkApi], calls);
        Assert.Equal(UpdateChannel.Fork, release.Channel);
        Assert.Equal("1.5.28", release.BaseVersion);
        Assert.Equal(2, release.ForkRevision);
        Assert.Equal("1.5.28-fork.2", release.Version);
        Assert.Equal(ForkDownloads + "TokenTracker-Setup.exe", release.SetupUrl);
        Assert.Equal(ForkDownloads + "SHA256SUMS", release.ChecksumsUrl);
    }

    [Fact]
    public async Task ForkChannelFallbackPinsAssetsToTheForkTag()
    {
        var calls = new List<(HttpMethod Method, string Url)>();
        using var http = Http(request =>
        {
            calls.Add((request.Method, request.RequestUri!.AbsoluteUri));
            return request.RequestUri.AbsoluteUri == ForkApi ? RateLimited() : PageResponse(request);
        });
        var resolver = new UpdateReleaseClient(http);

        var release = await resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None);

        Assert.True(resolver.UsedFallback);
        Assert.Equal("1.5.28-fork.2", release.Version);
        Assert.Equal(ForkDownloads + "TokenTracker-Setup.exe", release.SetupUrl);
        Assert.Equal(ForkDownloads + "SHA256SUMS", release.ChecksumsUrl);
        Assert.Equal(new[] {
            (HttpMethod.Get, ForkApi),
            (HttpMethod.Head, ForkPage),
            (HttpMethod.Head, release.SetupUrl),
            (HttpMethod.Get, release.ChecksumsUrl!),
        }, calls);
    }

    [Theory]
    [InlineData("v1.5.28")]            // no fork suffix: an official tag
    [InlineData("v1.5.28-rc.1")]
    [InlineData("v1.5.28-fork.01")]
    [InlineData("v1.5.28-fork.x")]
    [InlineData("v1.5.28-fork.")]
    [InlineData("v1.5.28-fork.1-rc.1")]
    public async Task ForkChannelRefusesTagsThatAreNotStableForkReleases(string tag)
    {
        using var http = Http(_ => ApiRelease(tag, ForkDownloads));
        var resolver = new UpdateReleaseClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None));
    }

    [Fact]
    public async Task OfficialChannelStillRefusesForkTags()
    {
        using var http = Http(_ => ApiRelease("v1.5.28-fork.1", ForkDownloads));
        var resolver = new UpdateReleaseClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => resolver.FetchLatestAsync(UpdateChannel.Official, CancellationToken.None));
    }

    [Fact]
    public async Task ForkChannelFallbackRejectsARedirectIntoAnotherRepository()
    {
        using var http = Http(request => request.RequestUri!.AbsoluteUri == ForkApi
            ? RateLimited()
            : PageRedirect("https://github.com/xiufengsun/TokenTracker/releases/tag/v1.5.28-fork.1"));
        var resolver = new UpdateReleaseClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None));
    }

    // ── the checker ────────────────────────────────────────────────────

    [Fact]
    public async Task AForkBuildFollowsTheForkByDefaultAndReportsItsForkVersion()
    {
        var calls = new List<string>();
        using var http = Http(request =>
        {
            calls.Add(request.RequestUri!.AbsoluteUri);
            return ApiRelease("v1.5.28-fork.2", ForkDownloads);
        });
        var checker = new UpdateChecker(http, installed: ForkBuild);

        Assert.Equal(UpdateChannel.Fork, checker.Channel);
        Assert.Equal("1.5.28-fork.1", checker.DisplayVersion);
        Assert.Equal("1.5.28", checker.CurrentVersion);
        Assert.True(checker.CanChooseChannel);
        Assert.Equal(UpdateChannel.Fork.ReleasePageUrl, checker.ReleasePageUrl);
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await checker.CheckAsync(silent: false));

        Assert.Equal([ForkApi], calls);
        Assert.Equal("1.5.28-fork.2", checker.LatestVersion);
        Assert.False(checker.PendingReleaseCrossesChannels);
    }

    [Fact]
    public async Task AForkBuildIsUpToDateOnItsOwnRelease()
    {
        using var http = Http(_ => ApiRelease("v1.5.28-fork.1", ForkDownloads));
        var checker = new UpdateChecker(http, installed: ForkBuild);

        Assert.Equal(UpdateChecker.CheckOutcome.UpToDate, await checker.CheckAsync(silent: false));
        Assert.Equal(UpdateChecker.UpdateState.Idle, checker.State);
        Assert.Null(checker.LatestVersion);
    }

    [Fact]
    public async Task ChoosingTheOfficialChannelOffersTheOfficialReleaseOfTheSameBaseVersion()
    {
        var calls = new List<string>();
        using var http = Http(request =>
        {
            calls.Add(request.RequestUri!.AbsoluteUri);
            return ApiRelease("v1.5.28", OfficialDownloads);
        });
        var checker = new UpdateChecker(http, _ => UpdateChannel.Official, ForkBuild);

        Assert.Equal(UpdateChannel.Official, checker.Channel);
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await checker.CheckAsync(silent: false));

        Assert.Equal([OfficialApi], calls);
        Assert.Equal("1.5.28", checker.LatestVersion);
        Assert.True(checker.PendingReleaseCrossesChannels);
        Assert.Equal(UpdateChannel.Official.ReleasePageUrl, checker.ReleasePageUrl);
    }

    [Fact]
    public async Task ChoosingTheOfficialChannelDoesNotOfferAnOlderOfficialRelease()
    {
        using var http = Http(_ => ApiRelease("v1.5.27", "https://github.com/xiufengsun/TokenTracker/releases/download/v1.5.27/"));
        var checker = new UpdateChecker(http, _ => UpdateChannel.Official, ForkBuild);

        Assert.Equal(UpdateChecker.CheckOutcome.UpToDate, await checker.CheckAsync(silent: false));
        Assert.False(checker.PendingReleaseCrossesChannels);
    }

    [Fact]
    public async Task ResetLetsASwitchedChannelBeReEvaluated()
    {
        using var http = Http(request => request.RequestUri!.AbsoluteUri == ForkApi
            ? ApiRelease("v1.5.28-fork.1", ForkDownloads)
            : ApiRelease("v1.5.28", OfficialDownloads));
        var channel = UpdateChannel.Fork;
        var checker = new UpdateChecker(http, _ => channel, ForkBuild);
        Assert.Equal(UpdateChecker.CheckOutcome.UpToDate, await checker.CheckAsync(silent: false));

        channel = UpdateChannel.Official;
        Assert.True(checker.Reset());
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await checker.CheckAsync(silent: false));
        Assert.True(checker.PendingReleaseCrossesChannels);

        Assert.True(checker.Reset());
        Assert.Equal(UpdateChecker.UpdateState.Idle, checker.State);
        Assert.Null(checker.LatestVersion);
        Assert.False(checker.PendingReleaseCrossesChannels);
    }

    [Fact]
    public async Task AReleaseFromAnotherChannelStaysMarkedUntilItIsInstalledOrForgotten()
    {
        using var http = Http(_ => ApiRelease("v1.5.29", OfficialDownloads));
        var checker = new UpdateChecker(http, _ => UpdateChannel.Official, ForkBuild);
        Assert.False(checker.PreparedReleaseCrossesChannels);

        await checker.CheckAsync(silent: false);

        // Installing it has to know it is a channel switch even while the state
        // is no longer UpdateAvailable, so that the tray can restore the default
        // channel at hand-over.
        Assert.True(checker.PreparedReleaseCrossesChannels);
        Assert.True(checker.PendingReleaseCrossesChannels);
        Assert.True(checker.Reset());
        Assert.False(checker.PreparedReleaseCrossesChannels);
    }

    [Fact]
    public async Task ResetIsRefusedWhileACheckIsInFlight()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            return await response.Task.WaitAsync(cancellationToken);
        }));
        var checker = new UpdateChecker(http, installed: ForkBuild);
        var check = checker.CheckAsync(silent: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(checker.Reset());

        response.SetResult(ApiRelease("v1.5.28-fork.2", ForkDownloads));
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await check);
    }

    [Fact]
    public void AnOfficialBuildHasASingleChannel()
    {
        using var http = Http(_ => throw new InvalidOperationException("no request expected"));
        var checker = new UpdateChecker(http, installed: OfficialBuild);

        Assert.Equal(UpdateChannel.Official, checker.Channel);
        Assert.False(checker.CanChooseChannel);
        Assert.Equal("1.5.28", checker.DisplayVersion);
    }

    // ── helpers ────────────────────────────────────────────────────────

    private static HttpResponseMessage ApiRelease(string tag, string downloadBase)
        => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
            tag_name = tag,
            assets = new[] {
                new { name = "TokenTracker-Setup.exe", browser_download_url = downloadBase + "TokenTracker-Setup.exe", size = 900 },
                new { name = "SHA256SUMS", browser_download_url = downloadBase + "SHA256SUMS", size = 90 },
            },
        })) };

    private static HttpResponseMessage RateLimited()
        => new(HttpStatusCode.Forbidden) { Content = new StringContent("API rate limit exceeded") };

    private static HttpResponseMessage PageRedirect(string redirect)
        => new(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(HttpMethod.Head, redirect) };

    private static HttpResponseMessage PageResponse(HttpRequestMessage request)
    {
        var url = request.RequestUri!.AbsoluteUri;
        if (url == ForkPage) return PageRedirect(ForkTagPage);
        if (url == ForkDownloads + "TokenTracker-Setup.exe")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content.Headers.ContentLength = 900;
            return response;
        }
        if (url == ForkDownloads + "SHA256SUMS")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('a', 64) + "  TokenTracker-Setup.exe\n") };
        throw new InvalidOperationException("Unexpected test request: " + request.RequestUri.AbsolutePath);
    }

    private static HttpClient Http(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new Handler((request, _) => Task.FromResult(respond(request))));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}

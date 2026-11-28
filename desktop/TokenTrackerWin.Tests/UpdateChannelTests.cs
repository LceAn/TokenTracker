using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using TokenTrackerWin;
using Xunit;

namespace TokenTrackerWin.Tests;

public sealed class UpdateChannelTests
{
    // A fork build numbered by the fork itself.
    private static readonly InstalledRelease ForkBuild = new(UpdateChannel.Fork, "2.0.0", null);
    // The fork's first release, numbered after upstream before the fork started its own numbering.
    private static readonly InstalledRelease LegacyForkBuild = new(UpdateChannel.Fork, "1.5.29", 1);
    private static readonly InstalledRelease OfficialBuild = new(UpdateChannel.Official, "1.5.28", null);

    private const string ForkApi = "https://api.github.com/repos/LceAn/TokenTracker/releases/latest";
    private const string ForkPage = "https://github.com/LceAn/TokenTracker/releases/latest";
    private const string ForkTagPage = "https://github.com/LceAn/TokenTracker/releases/tag/v2.0.1";
    private const string OfficialApi = "https://api.github.com/repos/xiufengsun/TokenTracker/releases/latest";
    private const string ForkDownloads = "https://github.com/LceAn/TokenTracker/releases/download/v2.0.1/";
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
    [InlineData("2.0.0+abc123", "fork", true, "2.0.0", null, "2.0.0")]
    [InlineData("2.0.0", "fork", true, "2.0.0", null, "2.0.0")]
    [InlineData("2.0.0+abc123", null, false, "2.0.0", null, "2.0.0")]          // unmarked: an official build that shares the number
    [InlineData("2.0.0+abc123", "official", false, "2.0.0", null, "2.0.0")]
    [InlineData("1.5.29-fork.1+abc123", null, true, "1.5.29", 1, "1.5.29-fork.1")]  // the fork's first release
    [InlineData("1.5.29-fork.12", "fork", true, "1.5.29", 12, "1.5.29-fork.12")]
    [InlineData("1.5.28+abc123", null, false, "1.5.28", null, "1.5.28")]
    [InlineData("1.0.0", null, false, "1.0.0", null, "1.0.0")]
    public void InformationalVersionCarriesTheChannelAndRevision(
        string informationalVersion, string? buildChannel, bool isFork, string version, int? revision, string display)
    {
        var installed = InstalledRelease.FromInformationalVersion(informationalVersion, "9.9.9", buildChannel);

        Assert.Equal(isFork ? UpdateChannel.Fork : UpdateChannel.Official, installed.Channel);
        Assert.Equal(version, installed.Version);
        Assert.Equal(revision, installed.ForkRevision);
        Assert.Equal(display, installed.DisplayVersion);
        Assert.Equal(revision is not null, installed.HasLegacyNumbering);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.5")]
    public void AnUnreadableVersionFallsBackToTheFallbackVersion(string? informationalVersion)
    {
        var unmarked = InstalledRelease.FromInformationalVersion(informationalVersion, "9.9.9");
        var marked = InstalledRelease.FromInformationalVersion(informationalVersion, "9.9.9", "fork");

        Assert.Equal(UpdateChannel.Official, unmarked.Channel);
        Assert.Equal(UpdateChannel.Fork, marked.Channel);   // the marker still decides the channel
        Assert.Equal("9.9.9", unmarked.Version);
        Assert.Equal("9.9.9", marked.Version);
        Assert.Null(marked.ForkRevision);
    }

    [Theory]
    [InlineData("2.0.0+abc123", "fork", true, "2.0.0")]
    [InlineData("2.0.0+abc123", null, false, "2.0.0")]     // unmarked: an official build that shares the number
    [InlineData("1.5.29-fork.1+abc123", null, true, "1.5.29")]
    public void TheChannelIsReadFromTheAssemblyAttributes(string informationalVersion, string? channel, bool isFork, string version)
    {
        // The test assembly itself must stay unmarked: upstream's tests construct
        // UpdateChecker from it and expect an official build.
        var probe = MarkedAssembly(informationalVersion, channel);

        var installed = InstalledRelease.FromAssembly(probe, "9.9.9");

        Assert.Equal(isFork ? UpdateChannel.Fork : UpdateChannel.Official, installed.Channel);
        Assert.Equal(version, installed.Version);
    }

    [Fact]
    public void TheForkBuildWritesTheMarkerTheRunningAppLooksFor()
    {
        var props = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TokenTrackerWin", "ForkBuild.props"));

        Assert.Contains($@"Include=""{InstalledRelease.ChannelMetadataKey}"" Value=""{UpdateChannel.Fork.Id}""", props);
    }

    [Fact]
    public void AnAssemblyWithoutTheMarkerIsAnOfficialBuild()
    {
        var installed = InstalledRelease.FromAssembly(typeof(object).Assembly, "9.9.9");

        Assert.Equal(UpdateChannel.Official, installed.Channel);
        Assert.Null(installed.ForkRevision);
        Assert.False(installed.HasLegacyNumbering);
    }

    // ── decision ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, "2.0.1", null, true)]        // a newer fork release
    [InlineData(true, "2.0.0", null, false)]       // this very release
    [InlineData(true, "1.9.9", null, false)]       // an older fork release is never an update
    [InlineData(false, "1.5.34", null, true)]      // the other channel is offered whatever its number
    [InlineData(false, "1.5.27", null, true)]
    public void AForkBuildIsOfferedNewerReleasesAndTheChosenChannelSwitches(
        bool releaseIsFork, string version, int? revision, bool expected)
    {
        var channel = releaseIsFork ? UpdateChannel.Fork : UpdateChannel.Official;

        Assert.Equal(expected, UpdateDecision.IsUpdate(ForkBuild, channel, version, revision));
    }

    [Theory]
    [InlineData("2.0.0", null, true)]        // the fork's own numbering: newer than every legacy release
    [InlineData("1.5.30", 1, true)]          // a newer upstream base
    [InlineData("1.5.29", 2, true)]          // a higher revision of the same base
    [InlineData("1.5.29", 1, false)]         // this very release
    [InlineData("1.5.28", 9, false)]         // an older base is never an update
    public void TheForksFirstReleaseIsOfferedEverythingPublishedSince(string version, int? revision, bool expected)
    {
        Assert.Equal(expected, UpdateDecision.IsUpdate(LegacyForkBuild, UpdateChannel.Fork, version, revision));
    }

    [Theory]
    [InlineData("1.5.29", true)]
    [InlineData("1.5.28", false)]
    [InlineData("1.5.27", false)]
    public void AnOfficialBuildIsOfferedNewerOfficialReleases(string version, bool expected)
    {
        Assert.Equal(expected, UpdateDecision.IsUpdate(OfficialBuild, UpdateChannel.Official, version, null));
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
            return ApiRelease("v2.0.1", ForkDownloads);
        });
        var resolver = new UpdateReleaseClient(http);

        var release = await resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None);

        Assert.Equal([ForkApi], calls);
        Assert.Equal(UpdateChannel.Fork, release.Channel);
        Assert.Equal("2.0.1", release.BaseVersion);
        Assert.Null(release.ForkRevision);
        Assert.Equal("2.0.1", release.Version);
        Assert.Equal(ForkDownloads + "TokenTracker-Setup.exe", release.SetupUrl);
        Assert.Equal(ForkDownloads + "SHA256SUMS", release.ChecksumsUrl);
    }

    [Fact]
    public async Task ForkChannelStillParsesTheTagOfTheForksFirstRelease()
    {
        using var http = Http(_ => ApiRelease("v1.5.29-fork.1", "https://github.com/LceAn/TokenTracker/releases/download/v1.5.29-fork.1/"));
        var resolver = new UpdateReleaseClient(http);

        var release = await resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None);

        Assert.Equal("1.5.29", release.BaseVersion);
        Assert.Equal(1, release.ForkRevision);
        Assert.Equal("1.5.29-fork.1", release.Version);
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
        Assert.Equal("2.0.1", release.Version);
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
    [InlineData("v2.0")]               // two parts
    [InlineData("v2.0.0-rc.1")]
    [InlineData("v2.0.0-fork.01")]
    [InlineData("v2.0.0-fork.x")]
    [InlineData("v2.0.0-fork.")]
    [InlineData("v2.0.0-fork.1-rc.1")]
    public async Task ForkChannelRefusesTagsThatAreNotStableReleases(string tag)
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
            : PageRedirect("https://github.com/xiufengsun/TokenTracker/releases/tag/v2.0.1"));
        var resolver = new UpdateReleaseClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => resolver.FetchLatestAsync(UpdateChannel.Fork, CancellationToken.None));
    }

    // ── the checker ────────────────────────────────────────────────────

    [Fact]
    public async Task AForkBuildFollowsTheForkByDefaultAndReportsItsOwnVersion()
    {
        var calls = new List<string>();
        using var http = Http(request =>
        {
            calls.Add(request.RequestUri!.AbsoluteUri);
            return ApiRelease("v2.0.1", ForkDownloads);
        });
        var checker = new UpdateChecker(http, installed: ForkBuild);

        Assert.Equal(UpdateChannel.Fork, checker.Channel);
        Assert.Equal("2.0.0", checker.DisplayVersion);
        Assert.Equal("2.0.0", checker.CurrentVersion);
        Assert.True(checker.CanChooseChannel);
        Assert.Equal(UpdateChannel.Fork.ReleasePageUrl, checker.ReleasePageUrl);
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await checker.CheckAsync(silent: false));

        Assert.Equal([ForkApi], calls);
        Assert.Equal("2.0.1", checker.LatestVersion);
        Assert.False(checker.PendingReleaseCrossesChannels);
    }

    [Fact]
    public async Task AForkBuildIsUpToDateOnItsOwnRelease()
    {
        using var http = Http(_ => ApiRelease("v2.0.0", ForkDownloads));
        var checker = new UpdateChecker(http, installed: ForkBuild);

        Assert.Equal(UpdateChecker.CheckOutcome.UpToDate, await checker.CheckAsync(silent: false));
        Assert.Equal(UpdateChecker.UpdateState.Idle, checker.State);
        Assert.Null(checker.LatestVersion);
    }

    [Fact]
    public async Task TheForksFirstReleaseIsOfferedTheFirstReleaseOfTheForksOwnNumbering()
    {
        using var http = Http(_ => ApiRelease("v2.0.0", ForkDownloads));
        var checker = new UpdateChecker(http, installed: LegacyForkBuild);

        Assert.Equal("1.5.29-fork.1", checker.DisplayVersion);
        Assert.Equal(UpdateChecker.CheckOutcome.UpdateAvailable, await checker.CheckAsync(silent: false));

        Assert.Equal("2.0.0", checker.LatestVersion);
        Assert.False(checker.PendingReleaseCrossesChannels);
    }

    [Fact]
    public async Task ChoosingTheOfficialChannelOffersTheOfficialReleaseWhateverItsNumber()
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
    public async Task ResetLetsASwitchedChannelBeReEvaluated()
    {
        using var http = Http(request => request.RequestUri!.AbsoluteUri == ForkApi
            ? ApiRelease("v2.0.0", ForkDownloads)
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

        response.SetResult(ApiRelease("v2.0.1", ForkDownloads));
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

    // An in-memory assembly that carries the attributes a build gets, so the
    // reader is exercised against real attributes.
    private static Assembly MarkedAssembly(string informationalVersion, string? channel)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TokenTracker.Tests.BuildProbe"), AssemblyBuilderAccess.Run);
        if (channel is not null)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(AssemblyMetadataAttribute).GetConstructor(new[] { typeof(string), typeof(string) })!,
                new object[] { InstalledRelease.ChannelMetadataKey, channel }));
        }
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!,
            new object[] { informationalVersion }));
        return assembly;
    }

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

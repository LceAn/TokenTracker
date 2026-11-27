using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TokenTrackerWin;

/// <summary>
/// A GitHub repository whose releases this app can update from: the official
/// project, or the community fork a fork build belongs to. Both publish the same
/// assets (<c>TokenTracker-Setup.exe</c> + <c>SHA256SUMS</c>) under the same
/// installer identity, so either one can replace the other in place.
/// </summary>
internal sealed record UpdateChannel(string Id, string OwnerRepo)
{
    public static readonly UpdateChannel Official = new("official", "xiufengsun/TokenTracker");
    public static readonly UpdateChannel Fork = new("fork", "LceAn/TokenTracker");

    public string ApiUrl => $"https://api.github.com/repos/{OwnerRepo}/releases/latest";
    public string ReleasePageUrl => $"https://github.com/{OwnerRepo}/releases/latest";
    public string ReleasesUrl => $"https://github.com/{OwnerRepo}/releases";

    /// <summary>Path prefix a <c>/releases/latest</c> redirect has to land on.</summary>
    public string TagPath => $"/{OwnerRepo}/releases/tag/";

    public string DownloadBase => $"https://github.com/{OwnerRepo}/releases/download/";

    /// <summary>
    /// Resolve a persisted id. Unknown values are rejected rather than mapped, so
    /// a damaged entry can never pick a channel on the user's behalf.
    /// </summary>
    public static bool TryFromId(string? id, out UpdateChannel channel)
    {
        if (id == Fork.Id)
        {
            channel = Fork;
            return true;
        }
        if (id == Official.Id)
        {
            channel = Official;
            return true;
        }
        channel = Official;
        return false;
    }
}

/// <summary>
/// The release this build was installed from, read from the assembly's
/// informational version. A fork build carries <c>x.y.z-fork.N</c>; anything
/// else is an official build, which keeps upstream's update behaviour.
/// </summary>
internal sealed record InstalledRelease(UpdateChannel Channel, string Version, int? ForkRevision)
{
    private static readonly Regex Shape = new(@"^(\d+\.\d+\.\d+)(-fork\.(\d+))?$", RegexOptions.CultureInvariant);

    /// <summary>What the tray and Settings show: "1.5.28-fork.1" for a fork build, "1.5.28" otherwise.</summary>
    public string DisplayVersion => ForkRevision is { } revision ? $"{Version}-fork.{revision}" : Version;

    /// <param name="informationalVersion">The assembly's informational version, if any.</param>
    /// <param name="fallbackVersion">Base version to use when it does not match either shape.</param>
    public static InstalledRelease FromInformationalVersion(string? informationalVersion, string fallbackVersion)
    {
        // "1.5.28-fork.1+<commit>" -> drop the build metadata after '+'.
        var match = Shape.Match((informationalVersion ?? "").Split('+')[0]);
        if (!match.Success) return new InstalledRelease(UpdateChannel.Official, fallbackVersion, null);
        var revision = int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? (int?)parsed : null;
        return new InstalledRelease(revision is null ? UpdateChannel.Official : UpdateChannel.Fork, match.Groups[1].Value, revision);
    }
}

/// <summary>Whether a published release should be offered to an installation.</summary>
internal static class UpdateDecision
{
    /// <param name="installed">The release this build came from.</param>
    /// <param name="channel">The channel the release was found in.</param>
    /// <param name="version">The release's base version ("1.5.28").</param>
    /// <param name="forkRevision">The release's fork revision, for a fork release.</param>
    public static bool IsUpdate(InstalledRelease installed, UpdateChannel channel, string version, int? forkRevision)
    {
        var order = UpdateChecker.CompareVersions(installed.Version, version);
        if (order > 0) return false;                        // never offer an older base version
        if (installed.Channel != channel) return true;      // the user chose to follow the other channel
        if (order < 0) return true;
        return forkRevision is { } latest && latest > (installed.ForkRevision ?? -1);
    }
}

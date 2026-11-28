using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
/// The release this build was installed from, read from the assembly. A fork
/// build is marked by its <see cref="ChannelMetadataKey"/> assembly metadata;
/// an unmarked build is an official one, which keeps upstream's update
/// behaviour. Builds from before the fork numbered its own releases carry an
/// <c>x.y.z-fork.N</c> informational version instead, and are still recognised.
/// </summary>
internal sealed record InstalledRelease(UpdateChannel Channel, string Version, int? ForkRevision)
{
    /// <summary>Assembly metadata that names the channel a build belongs to.</summary>
    internal const string ChannelMetadataKey = "TokenTrackerUpdateChannel";

    private static readonly Regex Shape = new(@"^(\d+\.\d+\.\d+)(-fork\.(\d+))?$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether this build predates the fork's own numbering. Until the fork's
    /// 2.0.0 it published "1.5.29-fork.1": the upstream version plus a revision.
    /// </summary>
    public bool HasLegacyNumbering => ForkRevision is not null;

    /// <summary>What the tray and Settings show: "2.0.0", or "1.5.29-fork.1" for a legacy fork build.</summary>
    public string DisplayVersion => ForkRevision is { } revision ? $"{Version}-fork.{revision}" : Version;

    /// <summary>Read the identity of the assembly that is running.</summary>
    public static InstalledRelease FromAssembly(Assembly assembly, string fallbackVersion)
    {
        var channel = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == ChannelMetadataKey)?.Value;
        return FromInformationalVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            fallbackVersion,
            channel);
    }

    /// <param name="informationalVersion">The assembly's informational version, if any.</param>
    /// <param name="fallbackVersion">Version to use when it does not match either shape.</param>
    /// <param name="buildChannel">
    /// Value of the assembly's <see cref="ChannelMetadataKey"/> metadata, if any: "fork" for a
    /// fork build numbered by the fork itself.
    /// </param>
    public static InstalledRelease FromInformationalVersion(
        string? informationalVersion, string fallbackVersion, string? buildChannel = null)
    {
        // "2.0.0+<commit>" -> drop the build metadata after '+'.
        var match = Shape.Match((informationalVersion ?? "").Split('+')[0]);
        var revision = match.Success
            && int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? (int?)parsed
                : null;
        var channel = revision is not null || buildChannel == UpdateChannel.Fork.Id
            ? UpdateChannel.Fork
            : UpdateChannel.Official;
        // The marker decides the channel even when the version is unreadable, so
        // a damaged fork build never quietly starts following the official one.
        return new InstalledRelease(channel, match.Success ? match.Groups[1].Value : fallbackVersion, revision);
    }
}

/// <summary>Whether a published release should be offered to an installation.</summary>
internal static class UpdateDecision
{
    /// <param name="installed">The release this build came from.</param>
    /// <param name="channel">The channel the release was found in.</param>
    /// <param name="version">The release's version without a legacy fork suffix ("2.0.1", "1.5.34").</param>
    /// <param name="forkRevision">The legacy fork revision, when the release carries one.</param>
    public static bool IsUpdate(InstalledRelease installed, UpdateChannel channel, string version, int? forkRevision)
    {
        // The two channels number their releases independently (the fork from
        // 2.0.0, upstream at 1.5.x), so their versions do not order against each
        // other. A release from the other channel is offered because the user
        // chose to follow that channel.
        if (installed.Channel != channel) return true;

        if (installed.ForkRevision is { } installedRevision)
        {
            // A build from before the fork numbered its own releases: every
            // release published since is newer than it.
            if (forkRevision is not { } latest) return true;
            var legacyOrder = UpdateChecker.CompareVersions(installed.Version, version);
            return legacyOrder < 0 || (legacyOrder == 0 && latest > installedRevision);
        }

        return UpdateChecker.CompareVersions(installed.Version, version) < 0;
    }
}

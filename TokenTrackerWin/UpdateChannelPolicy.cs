using System.IO;
using System.Text.Json.Nodes;

namespace TokenTrackerWin;

/// <summary>
/// Persists the user's update channel choice alongside the other native
/// settings. A missing or unreadable value means "follow the channel this build
/// came from": a fork build keeps following the fork, and an official build
/// keeps its historical behaviour, until the user picks something else.
/// </summary>
internal static class UpdateChannelPolicy
{
    public const string ChannelKey = "UpdateChecker.updateChannel";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TokenTracker",
        "native-settings.json");

    /// <summary>The channel to follow: the stored choice when it is valid, otherwise <paramref name="defaultChannel"/>.</summary>
    public static UpdateChannel GetSelected(UpdateChannel defaultChannel)
        => ResolveSelected(ReadSettings(), defaultChannel);

    internal static UpdateChannel ResolveSelected(JsonObject? settings, UpdateChannel defaultChannel)
    {
        if (settings?[ChannelKey] is JsonValue value
            && value.TryGetValue<string>(out var id)
            && UpdateChannel.TryFromId(id, out var channel))
        {
            return channel;
        }

        return defaultChannel;
    }

    public static void SetSelected(UpdateChannel channel)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var settings = ReadSettings();
            settings[ChannelKey] = channel.Id;
            File.WriteAllText(SettingsPath, settings.ToJsonString());
        }
        catch
        {
            // Preferences are best effort. A failed write leaves the default
            // channel in place for the next launch.
        }
    }

    private static JsonObject ReadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(SettingsPath))?.AsObject() ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }
}

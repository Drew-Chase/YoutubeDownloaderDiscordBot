using Chase.CommonLib.FileSystem.Configuration;
using Newtonsoft.Json;

namespace YoutubeDownloaderDiscordBot;

public sealed class BotConfiguration : AppConfigBase<BotConfiguration>
{
    [JsonProperty("token")]
    public string Token { get; set; }
}
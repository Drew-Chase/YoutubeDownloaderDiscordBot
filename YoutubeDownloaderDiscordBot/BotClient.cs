using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Serilog;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloaderDiscordBot;

public class BotClient
{
    private readonly DiscordSocketClient _client;
    private readonly YoutubeClient _youtubeClient;

    private BotClient()
    {
        _client = new DiscordSocketClient();
        _client.Log += HandleLogging;
        _client.Ready += InitializeSlashCommands;
        _youtubeClient = new YoutubeClient();
    }

    public static async Task<BotClient> Initialize()
    {
        BotClient botClient = new BotClient();
        await botClient._client.LoginAsync(TokenType.Bot, BotConfiguration.Instance.Token);
        await botClient._client.StartAsync();
        return botClient;
    }

    private static Task HandleLogging(LogMessage logMessage)
    {
        switch (logMessage.Severity)
        {
            case LogSeverity.Critical:
                Log.Fatal(logMessage.Message);
                break;
            case LogSeverity.Error:
                Log.Error(logMessage.Message);
                break;
            case LogSeverity.Warning:
                Log.Warning(logMessage.Message);
                break;
            case LogSeverity.Info:
                Log.Information(logMessage.Message);
                break;
            case LogSeverity.Debug:
                Log.Debug(logMessage.Message);
                break;
            default:
            case LogSeverity.Verbose:
                Log.Verbose(logMessage.Message);
                break;
        }

        return Task.CompletedTask;
    }

    private async Task InitializeSlashCommands()
    {
        var command = new SlashCommandBuilder();
        command.WithName("download")
            .WithDescription("Download a video from YouTube")
            .AddOption("url", ApplicationCommandOptionType.String, "The URL of the video to download", true);

        try
        {
            await _client.Rest.CreateGlobalCommand(command.Build());
        }
        catch (HttpException e)
        {
            Log.Error(e, "Failed to register slash command");
        }

        _client.SlashCommandExecuted += async slashCommand =>
        {
            string? url = slashCommand.Data.Options.FirstOrDefault(option => option.Name == "url")?.Value as string;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                await slashCommand.RespondAsync("Please provide a valid URL");
                return;
            }

            try
            {
                await slashCommand.DeferAsync(ephemeral: true);
                Video video = await _youtubeClient.Videos.GetAsync(url);
                StreamManifest manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);
                string title = video.Title;
                string author = video.Author.ChannelTitle;
                var duration = video.Duration;
                string[] qualityOptions = manifest.GetMuxedStreams().Select(i => i.VideoQuality.Label).ToArray();

                SelectMenuComponent? qualitySelection = new SelectMenuBuilder()
                    .WithCustomId("quality_selection")
                    .WithPlaceholder("Select quality")
                    .WithOptions(qualityOptions.Select(option => new SelectMenuOptionBuilder()
                        .WithLabel(option)
                        .WithValue(option)).ToList())
                    .Build();


                var embed = new EmbedBuilder()
                    .WithTitle(title)
                    .WithDescription($"By {author}")
                    .WithFooter($"Duration: {duration}")
                    .WithColor(Color.Red)
                    .WithUrl(url)
                    .WithImageUrl(video.Thumbnails.OrderByDescending(i => i.Resolution.Area).First().Url) // Get the highest resolution thumbnail
                    .Build();
                await slashCommand.RespondAsync("Select the quality of the video", embeds: [embed], ephemeral:true);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to download video");
                await slashCommand.RespondAsync("Failed to download video");
            }
        };
    }
}
using System.Diagnostics;
using Discord;
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
        command.WithName("archive")
            .WithDescription("Archive a video from YouTube")
            .AddOption("url", ApplicationCommandOptionType.String, "The URL of the video to archived", true);

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
            string method = slashCommand.Data.Options.First().Name;
            string? query = slashCommand.Data.Options.First().Value as string;

            Log.Information("Received command: {Method} with query: {Query}", method, query);

            switch (method)
            {
                case "url":
                    await HandleDownloadQueryCommand(query, slashCommand);
                    break;
            }
        };
    }

    private async Task HandleDownloadQueryCommand(string? url, SocketSlashCommand slashCommand)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            await slashCommand.RespondAsync("Please provide a valid URL");
            return;
        }

        try
        {
            await slashCommand.DeferAsync(ephemeral: true);
            Video video = await _youtubeClient.Videos.GetAsync(url);
            StreamManifest manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);
            string title = video.Title;
            string author = video.Author.ChannelTitle;
            var videoOptions = manifest.GetVideoOnlyStreams().GetWithHighestVideoQuality();
            var audioOptions = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            string directory = Path.Combine("youtube.com", $"@{author}");
            Directory.CreateDirectory(directory);
            string filenameVideo = Path.Combine(directory, "video.mp4");
            string filenameAudio = Path.Combine(directory, "audio.mp3");
            string filenameMuxed = Path.Combine(directory, $"{Chase.CommonLib.Strings.GetValidFileName(video.Title)}.mp4");

            var threads = new Task[2];
            threads[0] = Task.Run(() => _youtubeClient.Videos.Streams.DownloadAsync(videoOptions, filenameVideo));
            threads[1] = Task.Run(() => _youtubeClient.Videos.Streams.DownloadAsync(audioOptions, filenameAudio));
            await Task.WhenAll(threads);

            string command = $"-y -i \"{filenameVideo}\" -i \"{filenameAudio}\" -c:v copy -c:a aac -strict experimental \"{filenameMuxed}\"";
            Log.Debug("Executing command: {Command}", command);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = command,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Log.Error("Failed to mux video and audio");
                await slashCommand.FollowupAsync("Failed to mux video and audio", ephemeral: true);
                return;
            }

            File.Delete(filenameVideo);
            File.Delete(filenameAudio);


            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription($"By {author}")
                .WithFooter("Has been archived successfully!")
                .WithColor(Color.Green)
                .WithUrl(url)
                .WithImageUrl(video.Thumbnails.OrderByDescending(i => i.Resolution.Area).First().Url) // Get the highest resolution thumbnail
                .WithFields()
                .Build();

            await slashCommand.FollowupAsync(
                embeds: [embed],
                ephemeral: true
            );
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to download video");
            await slashCommand.FollowupAsync("Failed to download video");
        }
    }
}
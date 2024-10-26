using Chase.SerilogExtension;
using Serilog;

namespace YoutubeDownloaderDiscordBot;

internal class Program
{
    private static async Task Main()
    {
        Log.Logger = (await new LoggerConfiguration()
                .EnhancedWriteToFileAsync("logs", flushToDiskInterval: TimeSpan.FromSeconds(30)))
            .WriteTo.Console(outputTemplate: "[Youtube Downloader] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .AutoCloseAndFlush()
            .CreateLogger();
        BotConfiguration.Instance.Initialize("appsettings.json");

        Log.Information("Starting bot...");

        if (string.IsNullOrWhiteSpace(BotConfiguration.Instance.Token))
        {
            Log.Fatal("Bot token is not set. Please set the token in the appsettings.json file.");
            Environment.Exit(1);
            return;
        }
        
        BotClient botClient = await BotClient.Initialize();

        

        // Prevent the bot from exiting
        await Task.Delay(-1);
    }
}
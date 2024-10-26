using System.Diagnostics;
using Chase.SerilogExtension;
using Serilog;

namespace YoutubeDownloaderDiscordBot;

internal static class Program
{
    private static async Task Main()
    {
        Log.Logger = (await new LoggerConfiguration()
                .EnhancedWriteToFileAsync("logs", flushToDiskInterval: TimeSpan.FromSeconds(30)))
            .WriteTo.Console(outputTemplate: "[Youtube Downloader] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Verbose()
            .AutoCloseAndFlush()
            .CreateLogger();
        BotConfiguration.Instance.Initialize("appsettings.json");

        Log.Information("Starting bot...");

        Log.Information("Checking if ffmpeg is installed...");
        if (OperatingSystem.IsWindows())
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Log.Fatal("FFmpeg is not installed. Please install FFmpeg and add it to your PATH.");
                Environment.Exit(1);
                return;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Log.Fatal("FFmpeg is not installed. Please install FFmpeg.");
                Environment.Exit(1);
                return;
            }
        }

        Log.Information("FFmpeg is installed.");


        if (string.IsNullOrWhiteSpace(BotConfiguration.Instance.Token))
        {
            Log.Fatal("Bot token is not set. Please set the token in the appsettings.json file.");
            Environment.Exit(1);
            return;
        }

        await BotClient.Initialize();
        // Prevent the bot from exiting
        await Task.Delay(-1);
    }
}
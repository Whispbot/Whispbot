using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Logging
{
    public static class Logging
    {
        public static async Task ReadyAsync(DiscordSocketClient client)
        {
            Log(LogSeverity.Info, "Bot", $"Shard {client.ShardId} is ready!");
            await Task.CompletedTask;
        }

        public static void Debug(string message)
        {
            Log(LogSeverity.Debug, "Bot", message);
        }
        public static void Info(string message)
        {
            Log(LogSeverity.Info, "Bot", message);
        }
        public static void Warning(string message)
        {
            Log(LogSeverity.Warning, "Bot", message);
        }
        public static void Error(string message, Exception? exception = null)
        {
            Log(LogSeverity.Error, "Bot", message, exception);
        }

        public static void Debug(string source, string message)
        {
            Log(LogSeverity.Debug, source, message);
        }
        public static void Info(string source, string message)
        {
            Log(LogSeverity.Info, source, message);
        }
        public static void Warning(string source, string message)
        {
            Log(LogSeverity.Warning, source, message);
        }
        public static void Error(string source, string message, Exception? exception = null)
        {
            Log(LogSeverity.Error, source, message, exception);
        }

        public static void Log(string message)
        {
            Log(LogSeverity.Info, "Bot", message);
        }

        public static void Log(LogSeverity severity, string source, string message, Exception? exception = null)
        {
            Log(new LogMessage(severity, source, message, exception));
        }
        public static async Task LogAsync(LogMessage message)
        {
            Log(message);
            await Task.CompletedTask;
        }
        public static void Log(LogMessage message)
        {
            var severity = message.Severity switch
            {
                LogSeverity.Critical => LogEventLevel.Fatal,
                LogSeverity.Error => LogEventLevel.Error,
                LogSeverity.Warning => LogEventLevel.Warning,
                LogSeverity.Info => LogEventLevel.Information,
                LogSeverity.Verbose => LogEventLevel.Verbose,
                LogSeverity.Debug => LogEventLevel.Debug,
                _ => LogEventLevel.Information
            };

            var sourceColor = message.Source switch
            {
                "Discord" => LogStyles.Blue,
                "Gateway" => LogStyles.LightCyan,
                "Rest" => LogStyles.LightGreen,

                "Database" => LogStyles.Yellow,
                "Commands" => LogStyles.LightBlue,
                "Interacts" => LogStyles.LightRed,
                "Bot" => LogStyles.LightMagenta,
                "API" => LogStyles.Green,

                _ => LogStyles.Red
            };

            var source = $"[{sourceColor}{LogStyles.Bold}{message.Source}{LogStyles.Reset}]{new String(' ', Math.Max(0, 9 - message.Source.Length))}";

            Serilog.Log.Write(severity, message.Exception, "{Source} {Message}", source, message.Message);
        }
    }
}

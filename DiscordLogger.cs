using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;

namespace Whispbot
{
    public static class DiscordLogger
    {
        private static HttpClient _client = new();
        private static string? _webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
        private static bool hasWarned = false;

        public static async Task Log(MessageBuilder message)
        {
            if (string.IsNullOrEmpty(_webhookUrl))
            {
                if (!hasWarned)
                {
                    hasWarned = true;
                    Serilog.Log.Fatal("Discord webhook URL is not set. Please set the DISCORD_WEBHOOK_URL environment variable.");
                }

                return;
            }

            try
            {
                await _client.PostAsync(_webhookUrl, new StringContent(
                    JsonConvert.SerializeObject(message),
                    Encoding.UTF8,
                    "application/json"
                ));
            }
            catch { }
        }

        public static async Task Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            await Log(new MessageBuilder() { content = message });
        }

        public static async Task Log(object message)
        {
            await Log(message?.ToString() ?? "");
        }

        public static async Task LogError(string message, Exception exception)
        {
            await Log($"{message}\n```{exception.Message}\n```\n```\n{exception.StackTrace}\n```");
        }
    }
}

using Amazon.Runtime.Internal.Util;
using Discord;
using Discord.Rest;
using Discord.Webhook;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;

namespace Whispbot
{
    public static class DiscordLogger
    {
        private static DiscordWebhookClient? _client;
        private readonly static string? _webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
        private static bool _hasWarned = false;

        public static async Task Log(
            string? text = null,
            Embed[]? embeds = null,
            MessageFlags flags = MessageFlags.None
        )
        {
            if (string.IsNullOrEmpty(_webhookUrl)) { 
                if (!_hasWarned)
                {
                    _hasWarned = true;
                    Serilog.Log.Fatal("Discord webhook URL is not set. Please set the DISCORD_WEBHOOK_URL environment variable.");
                }

                return;
            }

            _client ??= new(_webhookUrl);

            await _client.SendMessageAsync(
                text: text,
                embeds: embeds,
                flags: flags
            );
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

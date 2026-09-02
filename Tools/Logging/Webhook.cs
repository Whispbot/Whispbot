using Amazon.Runtime.Internal.Util;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;
using Whispbot.Tools.Bot;

namespace Whispbot.Tools.Logging
{
    public static class WebhookLogger
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
                    Logging.Error("Bot", "Discord webhook URL is not set. Please set the DISCORD_WEBHOOK_URL environment variable.");
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

        private static readonly string _readyEmoji = "<:GreenDot:1314219460089085972>";
        private static readonly string _processingEmoji = "<:YellowDot:1314219457702395926>";
        private static readonly string _errorEmoji = "<:RedDot:1314219462244958228>";

        private enum ShardStatus
        {
            Connected,
            Connecting,
            Disconnected
        }

        private static string GenerateTimestamp()
        {
            var dto = DateTimeOffset.UtcNow;
            var secs = dto.ToUnixTimeSeconds();

            return $"[<t:{secs}:S>] ";
        }

        private static string Construct(DiscordSocketClient shard, ShardStatus status, Exception? ex = null)
        {
            var clusterId = Config.cluster;
            var shardId = shard.ShardId;
            var ts = GenerateTimestamp();

            // Ignore GatewayReconnectException as it is expected when a shard is asked to reconnect by Discord
            var exMessage = ex is not null && ex is not GatewayReconnectException ? $"\n```ts\n[{ex.GetType().FullName}] {ex.Message}\n{ex.StackTrace}\n```" : "";

            return ts + status switch
            {
                ShardStatus.Connected => $"{_readyEmoji} Shard `{shardId}` of cluster `{clusterId}` has connected",
                ShardStatus.Connecting => $"{_processingEmoji} Shard `{shardId}` of cluster `{clusterId}` is connecting",
                ShardStatus.Disconnected => $"{_errorEmoji} Shard `{shardId}` of cluster `{clusterId}` has disconnected{exMessage}",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        private static Task Log(DiscordSocketClient shard, ShardStatus status, Exception? ex = null)
        {
            // Prevents logging when all of the shards are disconnected
            // when the bot shuts down which is expected and not an error
            if (Shutdown.IsShuttingDown) return Task.CompletedTask;

            var message = Construct(shard, status, ex);

            return Log(message);
        }

        public static void Init(DiscordShardedClient client)
        {
            if (Config.isDev)
            {
                Logging.Warning("Bot", "Webhook init cancelled due to dev mode");
                return;
            }

            var ts = GenerateTimestamp();
            Task.Run(() => Log($"{ts} {_processingEmoji} Cluster `{Config.cluster}` is starting"));

            client.ShardConnected += async (shard) =>
            {
                await Log(shard, ShardStatus.Connected);
            };
            client.ShardDisconnected += async (ex, shard) =>
            {
                await Log(shard, ShardStatus.Disconnected, ex);
            };
            Shutdown.OnShuttingDown += async () =>
            {
                var ts = GenerateTimestamp();
                await Log($"{ts} {_errorEmoji} Cluster `{Config.cluster}` is shutting down");
            };
        }
    }
}

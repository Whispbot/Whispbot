using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools.Logger;

namespace Whispbot.Databases
{
    public static class DiscordPublisher
    {
        private static object? ConvertChannelToObject(SocketChannel rawChannel)
        {
            if (rawChannel is not SocketGuildChannel channel) return null;

            ulong? parentId = null;
            if (channel is INestedChannel nested) parentId = nested.CategoryId;

            return new
            {
                id = channel.Id,
                name = channel.Name,
                parent_id = parentId,
                guild_id = channel.Guild.Id,
                position = channel.Position,
                type = channel.ChannelType
            };
        }

        private static object ConvertRoleToObject(SocketRole role)
        {
            return new
            {
                id = role.Id,
                name = role.Name,
                position = role.Position,
                hoist = role.IsHoisted,
                color = role.Colors.PrimaryColor
            };
        }

        public static void Start(DiscordShardedClient client)
        {
            var publisher = Redis.GetSubscriber();
            int attempts = 2;
            while (publisher is null && attempts <= 5)
            {
                Thread.Sleep(1000 * attempts);
                publisher = Redis.GetSubscriber();
                attempts++;
            }

            if (publisher is null)
            {
                Log.Error($"Failed to connect to redis for discord publisher after multiple attempts");
                return;
            }

            // Channels
            client.ChannelCreated += async (channel) =>
            {
                var obj = ConvertChannelToObject(channel);
                if (obj is null) return;

                await publisher.PublishAsync("discord:channel:create", JsonConvert.SerializeObject(obj));
            };
            client.ChannelDestroyed += async (channel) =>
            {
                var obj = ConvertChannelToObject(channel);
                if (obj is null) return;

                await publisher.PublishAsync("discord:channel:delete", JsonConvert.SerializeObject(obj));
            };
            client.ChannelUpdated += async (_, newChannel) =>
            {
                var obj = ConvertChannelToObject(newChannel);
                if (obj is null) return;

                await publisher.PublishAsync("discord:channel:update", JsonConvert.SerializeObject(obj));
            };

            // Roles
            client.RoleCreated += async (role) =>
            {
                await publisher.PublishAsync("discord:role:create", JsonConvert.SerializeObject(new { role.Guild.Id, role = ConvertRoleToObject(role) }));
            };
            client.RoleDeleted += async (role) =>
            {
                await publisher.PublishAsync("discord:role:delete", JsonConvert.SerializeObject(role));
            };
            client.RoleUpdated += async (_, newRole) =>
            {
                await publisher.PublishAsync("discord:role:update", JsonConvert.SerializeObject(new { newRole.Guild.Id, role = ConvertRoleToObject(newRole) }));
            };

            Logging.Log($"Started discord publisher");
        }
    }
}

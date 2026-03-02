using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Sharding;

namespace Whispbot.Databases
{
    public static class DiscordPublisher
    {
        private static object ConvertChannelToObject(Channel channel)
        {
            return new
            {
                channel.id,
                channel.name,
                channel.parent_id,
                channel.guild_id,
                channel.position,
                channel.type
            };
        }

        private static object ConvertRoleToObject(Role role)
        {
            return new
            {
                role.id,
                role.name,
                role.position,
                role.hoist,
                role.color
            };
        }

        public static void Start(Client client)
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
                Log.Error($"Failed to connect to redis for discord publisher after multiple attempts for shard {client.shard?.id ?? 0}");
                return;
            }

            // Channels
            client.ChannelCreate += async (_, channel) =>
            {
                await publisher.PublishAsync("discord:channel:create", JsonConvert.SerializeObject(ConvertChannelToObject(channel)));
            };
            client.ChannelDelete += async (_, channel) =>
            {
                await publisher.PublishAsync("discord:channel:delete", JsonConvert.SerializeObject(new { channel.id, channel.guild_id }));
            };
            client.ChannelUpdate += async (_, channel) =>
            {
                await publisher.PublishAsync("discord:channel:update", JsonConvert.SerializeObject(ConvertChannelToObject(channel)));
            };

            // Roles
            client.GuildRoleCreate += async (_, role) =>
            {
                await publisher.PublishAsync("discord:role:create", JsonConvert.SerializeObject(new { role.guild_id, role = ConvertRoleToObject(role.role) }));
            };
            client.GuildRoleDelete += async (_, role) =>
            {
                await publisher.PublishAsync("discord:role:delete", JsonConvert.SerializeObject(role));
            };
            client.GuildRoleUpdate += async (_, role) =>
            {
                await publisher.PublishAsync("discord:role:update", JsonConvert.SerializeObject(new { role.guild_id, role = ConvertRoleToObject(role.role) }));
            };

            Log.Verbose($"Started discord publisher on shard {client.shard?.id}");
        }
        
        public static void Start(ShardingManager manager)
        {
            foreach (var shard in manager.shards)
            {
                Start(shard.client);
            }
        }
    }
}

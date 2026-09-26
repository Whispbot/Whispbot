using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Cache;

namespace Whispbot.Tools.Bot
{
    public static class Preloading
    {
        public static void Init(DiscordShardedClient client)
        {
            client.UserIsTyping += async (user, channel) =>
            {
                await WhispCache.UserConfig.Get(user.Id);

                var ch = channel.Value;
                if (ch is not null && ch is IGuildChannel gc)
                    await WhispCache.GuildConfig.Get(gc.GuildId);
            };
        }
    }
}

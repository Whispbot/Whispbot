using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Tools
{
    public static class Users
    {
        public static async Task<User?> GetUserByString(string input, string? inGuildId = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            if (input.StartsWith("<@")) input = input.Replace("<@", "").Replace("!", "").Replace(">", "");

            if (input.Length >= 17 && input.Length <= 20 && long.TryParse(input, out long _))
            {
                return await DiscordCache.Users.Get(input);
            }

            if (inGuildId is not null)
            {
                Guild? guild = await DiscordCache.Guilds.Get(inGuildId);
                if (guild is null) return null;

                return (await guild.SearchMembers(input))?.user;
            }

            return null;
        }
    }
}

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
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

        public static async Task<List<UserConfig>> GetConfigsFromRobloxIds(List<long> ids)
        {
            List<UserConfig>? userConfigs = WhispCache.UserConfig.FindMany((u, _) => ids.Contains(u.id));
            List<long> missingIds = [.. ids.Where(id => !userConfigs.Any(u => u.id == id))];
            if (missingIds.Count > 0)
            {
                List<UserConfig>? fetchedConfigs = Postgres.Select<UserConfig>(
                    @"SELECT * FROM user_config WHERE roblox_id IS NOT NULL AND roblox_id = ANY(@1);",
                    [missingIds]
                );
                if (fetchedConfigs is not null && fetchedConfigs.Count > 0)
                {
                    userConfigs.AddRange(fetchedConfigs);
                    foreach (var config in fetchedConfigs)
                    {
                        WhispCache.UserConfig.Insert(config.id.ToString(), config);
                    }
                }
            }

            return userConfigs;
        }

        public static async Task<List<Member>> GetMembersFromConfigs(List<UserConfig> configs, CommandContext ctx)
        {
            Guild? guild = ctx.Guild;
            if (guild is null) return [];

            List<Member>? members = null;
            if (configs.Count > 0)
            {
                members = guild.members.FindMany((m, _) => configs.Any(u => u.id.ToString() == m.user?.id));
                List<string> remainingMembers = [.. configs.Where(u => !members.Any(m => m.user!.id == u.id.ToString())).Select(u => u.id.ToString())];
                if (remainingMembers.Count > 0)
                {
                    members.AddRange(await guild.GetMembers(ctx.client, [.. configs.Select(u => u.id.ToString())]));
                }
            }

            return members ?? [];
        }
    }
}

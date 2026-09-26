using Discord;
using Discord.WebSocket;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Databases;

namespace Whispbot.Tools.Disc
{
    public static class Users
    {
        public static async Task<IUser?> GetUserByString(string input, int minLength = 0, ulong? inGuildId = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            if (input.StartsWith("<@")) input = input.Replace("<@", "").Replace("!", "").Replace(">", "");

            if (input.Length >= 17 && input.Length <= 20 && long.TryParse(input, out long _))
            {
                return await Config.client!.GetUserAsync(ulong.Parse(input), CacheMode.AllowDownload, RequestOptions.Default);
            }

            if (inGuildId is not null && input.Length >= minLength)
            {
                var guild = Config.client!.GetGuild(inGuildId.Value);
                if (guild is null) return null;

                var users = await guild.SearchUsersAsync(input, limit: 1);
                return users.FirstOrDefault();
            }

            return null;
        }

        public static async Task<List<UserConfig>> GetConfigsFromRobloxIds(List<ulong> ids)
        {
            List<UserConfig>? userConfigs = WhispCache.UserConfig.FindMany((u, _) => ids.Contains(u.id));
            List<ulong> missingIds = [.. ids.Where(id => !userConfigs.Any(u => u.id == id))];
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
                        WhispCache.UserConfig.Insert(config.id, config);
                    }
                }
            }

            return userConfigs;
        }

        public static async Task<List<SocketGuildUser>> GetMembersFromConfigs(List<UserConfig> configs, CommandContext ctx)
        {
            var guild = ctx.Guild;

            return [.. configs
                .Select(c => guild.GetUser(c.id))
                .Where(u => u is not null)];
        }

        public static readonly List<string> usernameEscapeChars = ["\\", "*", "_", "~", "`", ">", "|"];

        public static string FixUsername(string username)
        {
            foreach (string c in usernameEscapeChars)
            {
                username = username.Replace(c, $"\\{c}");
            }

            return username;
        }
    }
}

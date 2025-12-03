using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot
{
    public static partial class Procedures
    {
        public static async Task PostRMDelete(RobloxModeration moderation)
        {
            if (moderation.message_id is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id.ToString());
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);
            if (type is null) return;

            GuildConfig? config = await WhispCache.GuildConfig.Get(moderation.guild_id.ToString());
            if (config is null) return;

            long? log_channel_id = type.log_channel_id ?? config.roblox_moderation?.default_log_channel_id;
            if (log_channel_id is null) return;

            Message log = new()
            {
                id = moderation.message_id.ToString() ?? "",
                channel_id = log_channel_id.ToString() ?? ""
            };

            await log.Delete();
        }

        public static async Task<RobloxModeration?> DeleteRM(string guildId, string moderatorId, int caseId)
        {
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation;
            if (caseId == -1)
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "DELETE FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 AND \"case\" = (SELECT \"case\" FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY created_at DESC LIMIT 1) RETURNING *",
                    [long.Parse(guildId), long.Parse(moderatorId)]
                );
            }
            else if (caseId == -2)
            {
                if (hasAdminPerms)
                {
                    moderation = Postgres.SelectFirst<RobloxModeration>(
                        "DELETE FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = (SELECT \"case\" FROM roblox_moderations WHERE guild_id = @1 ORDER BY updated_at DESC LIMIT 1) RETURNING *",
                        [long.Parse(guildId)]
                    );
                }
                else moderation = null;
            }
            else
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    $"DELETE FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2{(hasAdminPerms ? "" : " AND moderator_id = @3")} RETURNING *",
                    [long.Parse(guildId), caseId, ..(hasAdminPerms ? [] : new List<long> { long.Parse(moderatorId) })]
                );
            }

            if (moderation is not null) _ = Task.Run(() => PostRMDelete(moderation));

            return moderation;
        }
    }
}

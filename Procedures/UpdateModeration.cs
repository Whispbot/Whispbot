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
        public static async Task PostRMModify(RobloxModeration moderation)
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

            await log.Edit(await GetRMLogMessage(moderation));
        }

        public static async Task<RobloxModeration?> ChangeRMReason(string guildId, string moderatorId, string reason, int caseId)
        {
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation = null;

            if (caseId == -1)
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    @"
                    UPDATE roblox_moderations
                    SET reason = @1, updated_at = NOW(), updated_by = @2
                    WHERE " + "\"case\"" + @" = (
                        SELECT " + "\"case\"" + @" FROM roblox_moderations
                        WHERE guild_id = @3 AND moderator_id = @2 AND is_deleted = FALSE
                        ORDER BY created_at DESC
                        LIMIT 1
                    ) AND guild_id = @3 AND moderator_id = @2
                    RETURNING *;
                    ",
                    [reason, long.Parse(moderatorId), long.Parse(guildId)]
                );
            }
            else if (caseId == -2)
            {
                if (hasAdminPerms)
                {
                    moderation = Postgres.SelectFirst<RobloxModeration>(
                        @"
                        UPDATE roblox_moderations
                        SET reason = @1, updated_at = NOW(), updated_by = @2
                        WHERE "" + ""\""case\"""" + @"" = (
                            SELECT "" + ""\""case\"""" + @"" FROM roblox_moderations
                            WHERE guild_id = @3
                            ORDER BY created_at DESC
                            LIMIT 1
                        ) AND guild_id = @3 AND is_deleted = FALSE
                        RETURNING *;
                        ",
                        [reason, long.Parse(moderatorId), long.Parse(guildId)]
                    );
                }
            }
            else
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    @"
                    UPDATE roblox_moderations
                    SET reason = @1, updated_at = NOW(), updated_by = @2
                    WHERE guild_id = @3 AND " + "\"case\"" + @$" = @4{(hasAdminPerms ? "" : " AND moderator_id = @2")} AND is_deleted = FALSE
                    RETURNING *;
                    ",
                    [reason, long.Parse(moderatorId), long.Parse(guildId), caseId]
                );
            }

            if (moderation is not null) _ = Task.Run(() => PostRMModify(moderation));

            return moderation;
        }

        public static async Task<RobloxModeration?> ChangeRMType(string guildId, string moderatorId, RobloxModerationType type, int caseId)
        {
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation = null;

            if (caseId == -1)
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    @"
                    UPDATE roblox_moderations
                    SET type = @1, updated_at = NOW(), updated_by = @2
                    WHERE " + "\"case\"" + @" = (
                        SELECT " + "\"case\"" + @" FROM roblox_moderations
                        WHERE guild_id = @3 AND moderator_id = @2
                        ORDER BY created_at DESC
                        LIMIT 1
                    ) AND guild_id = @3 AND moderator_id = @2 AND is_deleted = FALSE
                    RETURNING *;
                    ",
                    [type.id, long.Parse(moderatorId), long.Parse(guildId)]
                );
            }
            else if (caseId == -2)
            {
                if (hasAdminPerms)
                {
                    moderation = Postgres.SelectFirst<RobloxModeration>(
                        @"
                        UPDATE roblox_moderations
                        SET type = @1, updated_at = NOW(), updated_by = @2
                        WHERE "" + ""\""case\"""" + @"" = (
                            SELECT "" + ""\""case\"""" + @"" FROM roblox_moderations
                            WHERE guild_id = @3
                            ORDER BY created_at DESC
                            LIMIT 1
                        ) AND guild_id = @3 AND is_deleted = FALSE
                        RETURNING *;
                        ",
                        [type.id, long.Parse(moderatorId), long.Parse(guildId)]
                    );
                }
            }
            else
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    @"
                    UPDATE roblox_moderations
                    SET type = @1, updated_at = NOW(), updated_by = @2
                    WHERE guild_id = @3 AND " + "\"case\"" + @$" = @4{(hasAdminPerms ? "" : " AND moderator_id = @2")} AND is_deleted = FALSE
                    RETURNING *;
                    ",
                    [type.id, long.Parse(moderatorId), long.Parse(guildId), caseId]
                );
            }

            if (moderation is not null) _ = Task.Run(() => PostRMModify(moderation));

            return moderation;
        }
    }
}

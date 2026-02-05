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
        /// <summary>
        /// After modifying a Roblox moderation, update the log message
        /// </summary>
        /// <param name="moderation">The moderation which has just been edited</param>
        /// <returns></returns>
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

        /// <summary>
        /// Change the reason of a Roblox moderation
        /// </summary>
        /// <param name="guildId">The guild ID related to the case</param>
        /// <param name="moderatorId">The moderator who is modifying the case</param>
        /// <param name="reason">The new reason</param>
        /// <param name="caseId">The ID of the case (why did i make this the last param)</param>
        /// <returns>The modified <see cref="RobloxModeration"/> or null if failed</returns>
        public static async Task<RobloxModeration?> ChangeRMReason(string guildId, string moderatorId, string reason, int caseId)
        {
            // Decides if the moderator can edit cases at all
            bool hasDeletePerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseRobloxModerations);
            if (!hasDeletePerms) return null;

            // Decides if the moderator can edit other people's cases
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation = null;

            if (caseId == -1) // Edit own last case
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
            else if (caseId == -2) // Edit last case in guild (admin only)
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
            else // Edit specific case
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

        /// <summary>
        /// Change the type of a Roblox moderation
        /// </summary>
        /// <param name="guildId">The ID of the case's guild</param>
        /// <param name="moderatorId">The ID of the moderator updating this case</param>
        /// <param name="type">The new type</param>
        /// <param name="caseId">The ID of the case to be edited</param>
        /// <returns></returns>
        public static async Task<RobloxModeration?> ChangeRMType(string guildId, string moderatorId, RobloxModerationType type, int caseId)
        {
            // Decides if the moderator can edit cases at all
            bool hasDeletePerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseRobloxModerations);
            if (!hasDeletePerms) return null;

            // Decides if the moderator can edit other people's cases
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation = null;

            if (caseId == -1) // Edit own last case
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
            else if (caseId == -2) // Edit last case in guild (admin only)
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
            else // Edit specific case
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

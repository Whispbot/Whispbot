using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;

namespace Whispbot
{
    public static partial class Procedures
    {
        /// <summary>
        /// Deletes the log message for this case
        /// </summary>
        /// <param name="moderation">The deleted moderation</param>
        /// <returns></returns>
        public static async Task PostRMDelete(RobloxModeration moderation)
        {
            if (moderation.message_id is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);
            if (type is null) return;

            GuildConfig? config = await WhispCache.GuildConfig.Get(moderation.guild_id);
            if (config is null) return;

            ulong? log_channel_id = type.log_channel_id ?? config.roblox_moderation?.default_log_channel_id;
            if (log_channel_id is null) return;

            var guild = Config.client!.GetGuild(moderation.guild_id);
            var channel = guild?.GetTextChannel(log_channel_id.Value);
            if (channel is null) return;

            await channel.DeleteMessageAsync(moderation.message_id.Value);
        }

        /// <summary>
        /// Deletes a roblox moderation
        /// </summary>
        /// <param name="guildId">The guild which the case is in</param>
        /// <param name="moderatorId">The moderator who is deleting the case</param>
        /// <param name="caseId">The ID of the case to delete</param>
        /// <returns></returns>
        public static async Task<RobloxModeration?> DeleteRM(ulong guildId, ulong moderatorId, int caseId)
        {
            // Decides if the moderator can delete cases at all
            bool hasDeletePerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseRobloxModerations);
            if (!hasDeletePerms) return null;

            // Decides if the moderator can delete other people's cases
            bool hasAdminPerms = await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageRobloxModerations);

            RobloxModeration? moderation;
            if (caseId == -1) // Own last case
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "UPDATE roblox_moderations SET is_deleted = TRUE WHERE guild_id = @1 AND moderator_id = @2 AND \"case\" = (SELECT \"case\" FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY created_at DESC LIMIT 1) RETURNING *",
                    [guildId, moderatorId]
                );
            }
            else if (caseId == -2) // Server last case (admin only)
            {
                if (hasAdminPerms)
                {
                    moderation = Postgres.SelectFirst<RobloxModeration>(
                        "UPDATE roblox_moderations SET is_deleted = TRUE WHERE guild_id = @1 AND \"case\" = (SELECT \"case\" FROM roblox_moderations WHERE guild_id = @1 ORDER BY updated_at DESC LIMIT 1) RETURNING *",
                        [guildId]
                    );
                }
                else moderation = null;
            }
            else // Specific case
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    $"UPDATE roblox_moderations SET is_deleted = TRUE WHERE guild_id = @1 AND \"case\" = @2{(hasAdminPerms ? "" : " AND moderator_id = @3")} RETURNING *",
                    [guildId, caseId, ..(hasAdminPerms ? [] : new List<ulong> { moderatorId })]
                );
            }

            if (moderation is not null) _ = Task.Run(() => PostRMDelete(moderation));

            return moderation;
        }
    }
}

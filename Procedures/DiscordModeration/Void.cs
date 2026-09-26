using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Tools.Disc;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task DeleteMessage(DiscordModerationCase? modifiedCase)
        {
            if (modifiedCase?.message_id is not null)
            {
                var config = await WhispCache.GuildConfig.Get(modifiedCase.guild_id);
                if (config is null) return;

                var logChannelId = config.discord_moderation?.log_channel_id;
                if (logChannelId is not null)
                {
                    var guild = Config.client!.GetGuild(modifiedCase.guild_id);
                    var channel = guild.GetTextChannel(logChannelId.Value);
                    await channel.DeleteMessageAsync(modifiedCase.message_id.Value);
                }
            }
        }

        public static async Task<DiscordModerationCase?> VoidCase(IGuild guild, int caseId, IUser moderator)
        {
            var canUpdateAny = await DiscordPermissions.HasPermissionOrAdmin(
                guild,
                moderator.Id,
                GuildPermission.ManageGuild
            );

            DiscordModerationCase? modifiedCase = null;
            if (caseId < 0) // Edit last case, -1 = own, -2 = guild
            {
                modifiedCase = Postgres.SelectFirst<DiscordModerationCase>(
                    $@"
                    DELETE FROM discord_moderations
                    WHERE case_id = (
                        SELECT case_id FROM discord_moderations 
                        WHERE guild_id = @2{(caseId == -1 ? " AND moderator_id = @1" : "")}
                        ORDER BY created_at DESC
                        LIMIT 1
                    ) AND guild_id = @2
                    RETURNING *;
                    ",
                    [moderator.Id, guild.Id]
                );
            }
            else if (caseId > 0)
            {
                modifiedCase = Postgres.SelectFirst<DiscordModerationCase>(
                    $@"
                    DELETE FROM discord_moderations 
                    WHERE case_id = @2 AND guild_id = @3{(!canUpdateAny ? " AND moderator_id = @1" : "")}
                    RETURNING *;
                    ",
                    [moderator.Id, caseId, guild.Id]
                );
            }

            await DeleteMessage(modifiedCase);

            return modifiedCase;
        }
    }
}

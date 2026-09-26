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
        public static async Task UpdateMessage(DiscordModerationCase? modifiedCase)
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
                    await channel.ModifyMessageAsync(modifiedCase.message_id.Value, async m =>
                    {
                        m.Embed = await GenerateLogEmbed(modifiedCase);
                    });
                }
            }
        }

        public static async Task<DiscordModerationCase?> EditReason(IGuild guild, int caseId, IUser moderator, string newReason)
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
                    UPDATE discord_moderations
                    SET reason = @1, updated_at = now(), updated_by = @2 
                    WHERE case_id = (
                        SELECT case_id FROM discord_moderations 
                        WHERE guild_id = @3{(caseId == -1 ? " AND moderator_id = @2" : "")}
                        ORDER BY created_at DESC
                        LIMIT 1
                    ) AND guild_id = @3
                    RETURNING *;
                    ",
                    [newReason, moderator.Id, guild.Id]
                );
            }
            else if (caseId > 0)
            {
                modifiedCase = Postgres.SelectFirst<DiscordModerationCase>(
                    $@"
                    UPDATE discord_moderations 
                    SET reason = @1, updated_at = now(), updated_by = @2 
                    WHERE case_id = @3 AND guild_id = @4{(!canUpdateAny ? " AND moderator_id = @2" : "")}
                    RETURNING *;
                    ",
                    [newReason, moderator.Id, caseId, guild.Id]
                );
            }

            await DeleteMessage(modifiedCase);

            return modifiedCase;
        }
    }
}

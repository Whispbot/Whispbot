using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools.Discord;
using YellowMacaroni.Discord.Core;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task UpdateMessage(DiscordModerationCase? modifiedCase)
        {
            if (modifiedCase?.message_id is not null)
            {
                var config = await WhispCache.GuildConfig.Get(modifiedCase.guild_id.ToString());
                if (config is null) return;

                var logChannelId = config.discord_moderation?.log_channel_id;
                if (logChannelId is not null)
                {
                    var logMessage = new Message
                    { // who has time to fetch a message if you can just pretend you did
                        id = modifiedCase.message_id.ToString()!,
                        channel_id = logChannelId.ToString()!
                    };
                    await logMessage.Edit(await GenerateLogMessage(modifiedCase));
                }
            }
        }

        public static async Task<DiscordModerationCase?> EditReason(Guild guild, int caseId, User moderator, string newReason)
        {
            var canUpdateAny = await DiscordPermissions.HasPermissionOrAdmin(
                guild, 
                moderator.id, 
                Permissions.ManageGuild
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
                    [newReason, long.Parse(moderator.id), long.Parse(guild.id)]
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
                    [newReason, long.Parse(moderator.id), caseId, long.Parse(guild.id)]
                );
            }

            await DeleteMessage(modifiedCase);

            return modifiedCase;
        }
    }
}

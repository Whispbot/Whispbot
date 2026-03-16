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
        public static async Task DeleteMessage(DiscordModerationCase? modifiedCase)
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
                    await logMessage.Delete();
                }
            }
        }

        public static async Task<DiscordModerationCase?> VoidCase(Guild guild, int caseId, User moderator)
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
                    DELETE FROM discord_moderations
                    WHERE case_id = (
                        SELECT case_id FROM discord_moderations 
                        WHERE guild_id = @2{(caseId == -1 ? " AND moderator_id = @1" : "")}
                        ORDER BY created_at DESC
                        LIMIT 1
                    ) AND guild_id = @2
                    RETURNING *;
                    ",
                    [long.Parse(moderator.id), long.Parse(guild.id)]
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
                    [long.Parse(moderator.id), caseId, long.Parse(guild.id)]
                );
            }

            await DeleteMessage(modifiedCase);

            return modifiedCase;
        }
    }
}

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public async static Task PostClockout(long guildId, long moderatorId, ShiftType type, Shift shift, long? adminId = null)
        {
            Guild? thisGuild = await DiscordCache.Guilds.Get(guildId.ToString());
            if (thisGuild is null) return;

            Member? moderator = await thisGuild.members.Get(moderatorId.ToString());

            if (type.role_id is not null && moderator is not null)
            {
                Task _ = moderator.RemoveRole(type.role_id?.ToString() ?? "", $"Clocked out of shift type '{type.name}'.");
            }

            GuildConfig? config = await WhispCache.GuildConfig.Get(guildId.ToString());
            if (config is null) return;

            string? logChannelId = (type.log_channel_id ?? config.shifts?.default_log_channel_id)?.ToString();
            if (logChannelId is null) return;

            Channel? logChannel = await DiscordCache.Channels.Get(logChannelId);
            if (logChannel is null) return;

            Task __ = logChannel.Send(new MessageBuilder()
            {
                embeds = [
                    new EmbedBuilder()
                    {
                        author = new EmbedAuthor()
                        {
                            name = $"@{moderator?.user?.username ?? "err"} ({moderatorId})",
                            icon_url = moderator?.avatar_url ?? moderator?.user?.avatar_url
                        },
                        title = "{string.title.clockout}".Process((Tools.Strings.Language)(config.default_language ?? 0)),
                        description = $"<@{moderatorId}> {"{string.content.clockout}".Process((Tools.Strings.Language)(config.default_language ?? 0), new Dictionary<string, string> {
                            { "type_name", type.name },
                            { "duration", Time.ConvertMillisecondsToString((shift.end_time - shift.start_time)?.TotalMilliseconds ?? 0) }
                        })}.",
                        fields = adminId is null ? [] : [
                            new EmbedField
                            {
                                name = "{string.title.clockout.admin}",
                                value = $"<@{adminId}>"
                            }
                        ],
                        color = (int)(new Color(150, 0, 0)),
                        footer = new EmbedFooter() { text = $"ID: {shift.id}" }
                    }
                ]
            });
        }

        public static async Task<(Shift?, string?)> Clockout(long guildId, long moderatorId, ShiftType type, long? adminId = null)
        {
            if (adminId is not null && !await WhispPermissions.HasPermission(guildId.ToString(), (adminId ?? 0).ToString(), BotPermissions.ManageShifts))
            {
                return (null, "{string.errors.clockin.adminnoperms}");
            }

            Shift? thisShift;
            try
            {
                thisShift = Postgres.SelectFirst<Shift>(
                    @"UPDATE shifts SET end_time = now() WHERE moderator_id = @1 AND type = @2 AND end_time IS NULL RETURNING *",
                    [moderatorId, type.id]
                );
            }
            catch
            {
                return (null, null);
            }

            if (thisShift is null)
            {
                return (null, adminId is null ? "{string.errors.clockout.notclockedin}" : "{string.errors.clockout.usernotclockedin}");
            }

            _ = PostClockout(guildId, moderatorId, type, thisShift, adminId);

            return (thisShift, null);
        }
    }
}

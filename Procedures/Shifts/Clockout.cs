using Discord;
using Discord.WebSocket;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools;

namespace Whispbot
{
    public static partial class Procedures
    {
        public async static Task PostClockout(ulong guildId, ulong moderatorId, ShiftType type, Shift shift, ulong? adminId = null)
        {
            SocketGuild? thisGuild = Config.client!.GetGuild(guildId);
            if (thisGuild is null) return;

            SocketGuildUser moderator = thisGuild.GetUser(moderatorId);

            if (type.role_id is not null)
            {
                await moderator.RemoveRoleAsync(type.role_id!.Value, new RequestOptions { AuditLogReason = $"Clocked out of shift type '{type.name}'." });
            }

            GuildConfig? config = await WhispCache.GuildConfig.Get(guildId);
            if (config is null) return;

            ulong? logChannelId = (type.log_channel_id ?? config.shifts?.default_log_channel_id);
            if (logChannelId is null) return;

            SocketTextChannel? logChannel = thisGuild.GetTextChannel(logChannelId.Value);
            if (logChannel is null) return;

            var language = config.default_language ?? 0;

            await logChannel.SendMessageAsync(
                embed:
                    new EmbedBuilder()
                        .WithAuthor($"{moderator.Username} ({moderatorId})", moderator.GetDisplayAvatarUrl())
                        .WithTitle("shifts.clockout.log.title".Translate(language))
                        .WithDescription("shifts.clockout.log.content".Translate(language, $"<@{moderatorId}>", type.name, Time.ConvertMillisecondsToString((shift.end_time - shift.start_time)?.TotalMilliseconds ?? 0, language: language)))
                        .WithFields(adminId is null ? [] : [
                            new EmbedFieldBuilder()
                                .WithName("shifts.clockout.log.admin".Translate(language))
                                .WithValue($"<@{adminId}>")
                        ])
                        .WithColor(new Color(150, 0, 0))
                        .WithFooter($"ID: {shift.id} • {"phrase.type".Translate(language)}: {type.id}")
                        .Build()
            );
        }

        public static async Task<(Shift?, string?)> Clockout(ulong guildId, ulong moderatorId, ShiftType type, ulong? adminId = null)
        {
            if (adminId is not null && !await WhispPermissions.HasPermission(guildId, (adminId ?? 0), BotPermissions.ManageShifts))
            {
                return (null, "admin_no_perms");
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
                return (null, adminId is null ? "not_on_shift" : "user_not_on_shift");
            }

            _ = PostClockout(guildId, moderatorId, type, thisShift, adminId);

            return (thisShift, null);
        }
    }
}

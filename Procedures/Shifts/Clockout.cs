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

            await logChannel.SendMessageAsync(
                embed:
                    new EmbedBuilder()
                        .WithAuthor($"{moderator.Username} ({moderatorId})")
                        .WithTitle("{string.title.clockout}")
                        .WithDescription($"<@{moderatorId}> {"{string.content.clockout}".Process((Tools.Strings.Language)(config.default_language ?? 0), new Dictionary<string, string> {
                            { "type_name", type.name },
                            { "duration", Time.ConvertMillisecondsToString((shift.end_time - shift.start_time)?.TotalMilliseconds ?? 0) }
                        })}.")
                        .WithFields(adminId is null ? [] : [
                            new EmbedFieldBuilder()
                                .WithName("{string.title.clockout.admin}")
                                .WithValue($"<@{adminId}>")
                        ])
                        .WithColor(new Color(150, 0, 0))
                        .WithFooter($"ID: {shift.id}")
                        .Build()
                        .ProcessObj((Strings.Language)(config.default_language ?? 0))
            );
        }

        public static async Task<(Shift?, string?)> Clockout(ulong guildId, ulong moderatorId, ShiftType type, ulong? adminId = null)
        {
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.Shifts)).Item1) return (null, "{string.errors.clockin.moduledisabled}");

            if (adminId is not null && !await WhispPermissions.HasPermission(guildId, (adminId ?? 0), BotPermissions.ManageShifts))
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

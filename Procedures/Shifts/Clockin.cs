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
using Discord;

namespace Whispbot
{
    public static partial class Procedures
    {
        public async static Task PostClockin(ulong guildId, ulong moderatorId, ShiftType type, Shift shift, ulong? adminId = null)
        {
            var thisGuild = Config.client!.GetGuild(guildId);

            var moderator = thisGuild.GetUser(moderatorId);
            if (moderator is null) return;

            if (type.role_id is not null)
            {
                await moderator.AddRoleAsync(type.role_id!.Value, new RequestOptions { AuditLogReason = $"Clocked in to shift type '{type.name}'." });
            }

            var config = await WhispCache.GuildConfig.Get(guildId);
            if (config is null) return;

            var logChannelId = (type.log_channel_id ?? config.shifts?.default_log_channel_id);
            if (logChannelId is null) return;

            var logChannel = thisGuild.GetTextChannel(logChannelId.Value);
            if (logChannel is null) return;

            await logChannel.SendMessageAsync(
                embed: new EmbedBuilder()
                    .WithAuthor($"@{moderator.Username} ({moderatorId})", moderator.GetDisplayAvatarUrl())
                    .WithTitle("{string.title.clockin}")
                    .WithDescription($"<@{moderatorId}> {{string.content.clockin}} '{type.name}'.")
                    .WithFields(adminId is null ? [] : [new EmbedFieldBuilder().WithName("{string.title.clockin.admin}").WithValue($"<@{adminId}>")])
                    .WithColor(new Color(0, 150, 0))
                    .WithFooter($"ID: {shift.id}")
                    .Build()
            );
        }

        public static async Task<(Shift?, string?)> Clockin(ulong guildId, ulong moderatorId, ShiftType type, ulong? adminId = null)
        {
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.Shifts)).Item1) return (null, "{string.errors.clockin.moduledisabled}");

            if (type.is_deleted)
            {
                return (null, "{string.errors.clockin.invalidtype}");
            }

            if (adminId is not null && !await WhispPermissions.HasPermission(guildId, (adminId ?? 0), BotPermissions.ManageShifts))
            {
                return (null, "{string.errors.clockin.adminnoperms}");
            }

            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseShifts))
            {
                return (null, adminId is null ? "{string.errors.clockin.noperms}" : "{string.errors.clockin.usernoperms}");
            }

            if ((type.required_roles ?? []).Count > 0)
            {
                IGuild? guild = Config.client!.GetGuild(guildId);

                IGuildUser? moderator = await guild.GetUserAsync(moderatorId);
                if (moderator is null) return (null, "{string.errors.clockin.nomember}");

                if (!(moderator.RoleIds ?? []).Any(r => type.required_roles!.Contains(r))) return (null, adminId is null ? "{string.errors.clockin.missingrole}" : "{string.errors.clockin.usermissingrole}");
            }

            Shift? thisShift = null;
            try
            {
                thisShift = Postgres.SelectFirst<Shift>(
                    @"INSERT INTO shifts (guild_id, moderator_id, type) VALUES (@1, @2, @3) RETURNING *;",
                    [guildId, moderatorId, type.id]
                );
            }
            catch (Exception ex)
            {
                if (ex.Data["SqlState"]?.ToString() == "23505")
                {
                    return (null, adminId is null ? "{string.errors.clockin.already}" : "{string.errors.clockin.useralready}");
                }
            }

            if (thisShift is null)
            {
                return (null, "{string.errors.clockin.dbfailed}");
            }

            _ = PostClockin(guildId, moderatorId, type, thisShift, adminId);

            return (thisShift, null);
        }
    }

    public class Shift
    {
        public ulong id = 0;
        public ulong guild_id = 0;
        public ulong moderator_id = 0;
        public ulong type = 0;
        public DateTimeOffset start_time = DateTimeOffset.UtcNow;
        public DateTimeOffset? end_time = null;
    }
}

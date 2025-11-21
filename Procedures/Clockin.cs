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
        public async static Task PostClockin(long guildId, long moderatorId, ShiftType type, Shift shift, long? adminId = null)
        {
            Guild? thisGuild = await DiscordCache.Guilds.Get(guildId.ToString());
            if (thisGuild is null) return;

            Member? moderator = await thisGuild.members.Get(moderatorId.ToString());
            if (moderator is null) return;

            if (type.role_id is not null)
            {
                Task _ = moderator.AddRole(type.role_id?.ToString() ?? "", $"Clocked in to shift type '{type.name}'.");
            }

            ShiftConfig? shiftConfig = await WhispCache.ShiftConfig.Get(guildId.ToString());
            if (shiftConfig is null) return;

            string? logChannelId = (type.log_channel_id ?? shiftConfig?.default_log_channel_id)?.ToString();
            if (logChannelId is null) return;

            Channel? logChannel = await DiscordCache.Channels.Get(logChannelId);
            if (logChannel is null) return;

            GuildConfig? config = await WhispCache.GuildConfig.Get(guildId.ToString());
            if (config is null) return;

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
                        title = "{string.title.clockin}".Process((Tools.Strings.Language)(config.default_language ?? 0)),
                        description = $"<@{moderatorId}> {"{string.content.clockin}".Process((Tools.Strings.Language)(config.default_language ?? 0))} '{type.name}'.",
                        fields = adminId is null ? [] : [
                            new EmbedField
                            {
                                name = "{string.title.clockin.admin}",
                                value = $"<@{adminId}>"
                            }
                        ],
                        color = (int)(new Color(0, 150, 0)),
                        footer = new EmbedFooter() { text = $"ID: {shift.id}" }
                    }
                ]
            });
        }

        public static async Task<(Shift?, string?)> Clockin(long guildId, long moderatorId, ShiftType type, long? adminId = null)
        {
            if (adminId is not null && !await WhispPermissions.HasPermission(guildId.ToString(), (adminId ?? 0).ToString(), BotPermissions.ManageShifts))
            {
                return (null, "{string.errors.clockin.adminnoperms}");
            }

            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.UseShifts))
            {
                return (null, adminId is null ? "{string.errors.clockin.noperms}" : "{string.errors.clockin.usernoperms}");
            }

            if ((type.required_roles ?? []).Count > 0)
            {
                Guild? guild = await DiscordCache.Guilds.Get(guildId.ToString());
                if (guild is null) return (null, "{string.errors.clockin.noguild}");

                Member? moderator = await guild.members.Get(moderatorId.ToString());
                if (moderator is null) return (null, "{string.errors.clockin.nomember}");

                if (!(moderator.roles ?? []).Any(r => type.required_roles!.Contains(r))) return (null, adminId is null ? "{string.errors.clockin.missingrole}" : "{string.errors.clockin.usermissingrole}");
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
        public long id = 0;
        public long guild_id = 0;
        public long moderator_id = 0;
        public long type = 0;
        public DateTimeOffset start_time = DateTimeOffset.UtcNow;
        public DateTimeOffset? end_time = null;
    }
}

using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Shifts
{
    public class ShiftAdmin : Command
    {
        public override string Name => "Shift Admin";
        public override string Description => "Manage a user's shifts.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift admin"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ManageShifts)) return;

            User? user = ctx.args.Count > 0 ? await Users.GetUserByString(ctx.args[0], ctx.GuildId) : ctx.User;
            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {strings.errors.general.invaliduser}");
                return;
            }

            List<ShiftType>? shiftTypes = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (shiftTypes is null)
            {
                await ctx.Reply("{emoji.cross} {strings.errors.clockin.dbfailed}");
                return;
            }

            ShiftType? type = ctx.args.Count > 1 ? shiftTypes.Find(t => t.triggers.Contains(ctx.args[1])) : null;

            MessageBuilder message = await ShiftAdminMessages.GetMainMessage(ctx.GuildId, user.id, ctx.UserId, type);
            await ctx.Reply(message);
        }
    }

    public class ShiftAdminData
    {
        public int totalCount;
        public double totalDuration;
        public int weeklyCount;
        public double weeklyDuration;
        public float weeklyDurationIncreasePercent;
        public DateTimeOffset? currentShiftStart;
        public string recentShifts = "[]";
    }

    public static class ShiftAdminMessages
    {
        public static async Task<MessageBuilder> GetMainMessage(string guildId, string userId, string adminId, ShiftType? type = null)
        {
            Task<User?> userTask = DiscordCache.Users.Get(userId);
            Task<List<ShiftType>?> typesTask = WhispCache.ShiftTypes.Get(guildId);
            ShiftAdminData? data = Postgres.SelectFirst<ShiftAdminData>(@"
                SELECT
                    COUNT(*) AS totalCount,
                    COALESCE(SUM(EXTRACT(EPOCH FROM (COALESCE(s.end_time, now()) - s.start_time))), 0) AS totalDuration,
                    COUNT(CASE WHEN s.start_time >= now() - INTERVAL '7 days' THEN 1 END) AS weeklyCount,
                    COALESCE(SUM(CASE WHEN s.start_time >= now() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (COALESCE(s.end_time, now()) - s.start_time)) END), 0) AS weeklyDuration,
                    CASE
                        WHEN COALESCE(SUM(CASE WHEN s.start_time >= NOW() - INTERVAL '14 days' AND s.start_time < NOW() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (s.end_time - s.start_time)) END), 0) = 0 THEN 
                            CASE 
                                WHEN COALESCE(SUM(CASE WHEN s.start_time >= NOW() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (s.end_time - s.start_time)) END), 0) = 0 THEN 0.0
                                ELSE 100.0
                            END
                        ELSE
                            (
                                (COALESCE(SUM(CASE WHEN s.start_time >= NOW() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (s.end_time - s.start_time)) END), 0) -
                                COALESCE(SUM(CASE WHEN s.start_time >= NOW() - INTERVAL '14 days' AND s.start_time < NOW() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (s.end_time - s.start_time)) END), 0))
                                /
                                COALESCE(SUM(CASE WHEN s.start_time >= NOW() - INTERVAL '14 days' AND s.start_time < NOW() - INTERVAL '7 days' THEN EXTRACT(EPOCH FROM (s.end_time - s.start_time)) END), 0)
                            ) * 100.0
                    END AS weeklyDurationIncreasePercent,
                    (SELECT s2.start_time FROM shifts s2 WHERE s2.moderator_id = @1 AND s2.guild_id = @2" + (type is not null ? " AND s2.type = @3" : "") + @" AND s2.end_time IS NULL LIMIT 1) AS currentShiftStart,
                    COALESCE((
                        SELECT json_agg(
                            json_build_object(
                                'id', recent.id,
                                'guild_id', recent.guild_id,
                                'moderator_id', recent.moderator_id,
                                'type', recent.type,
                                'start_time', recent.start_time,
                                'end_time', recent.end_time
                            )
                        )
                        FROM (
                            SELECT s3.id, s3.guild_id, s3.moderator_id, s3.type, s3.start_time, s3.end_time
                            FROM shifts s3
                            WHERE s3.moderator_id = @1 AND s3.guild_id = @2" + (type is not null ? " AND s3.type = @3" : "") + @"
                            ORDER BY s3.start_time DESC
                            LIMIT 5
                        ) recent
                    ), '[]'::json) AS recentShifts
                FROM shifts s
                WHERE s.moderator_id = @1 AND s.guild_id = @2" + (type is not null ? " AND s.type = @3" : "") + @"; 
            ", [long.Parse(userId), long.Parse(guildId), ..(type is not null ? new List<long> { type.id } : [])]);

            if (data is null)
            {
                return new MessageBuilder
                {
                    content = "{emoji.warning} {strings.errors.shiftadmin.failedgetdata}"
                };
            }

            float percent = MathF.Abs(MathF.Round(data.weeklyDurationIncreasePercent * 10f) / 10f);
            bool increase = data.weeklyDurationIncreasePercent > 0;
            bool decrease = data.weeklyDurationIncreasePercent < 0;

            List<ShiftType>? types = await typesTask;
            List<Shift> recentShifts = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Shift>>(data.recentShifts) ?? [];
            User? user = await userTask;

            List<TextDisplay> topComponents = [
                new TextDisplayBuilder($"## {{strings.title.shiftadmin}}\n-# @{user?.username ?? "unknown"}"),
                new TextDisplayBuilder($"**{{strings.title.shift.alltime}}:** {data.totalCount} ({Time.ConvertMillisecondsToString(data.totalDuration * 1000, ", ", true)})\n**{{strings.title.shift.weekly}}:** {data.weeklyCount} ({Time.ConvertMillisecondsToString(data.weeklyDuration * 1000, ", ", true)})\n**{{strings.title.shiftadmin.trend}}**: {{strings.content.shiftadmin.{(increase ? "increase" : decrease ? "decrease" : "same")}:percent={percent}}}"),
            ];

            return new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [
                            ..(user?.avatar_url is not null ? new List<Component> {
                                new SectionBuilder {
                                    components = topComponents,
                                    accessory = new ThumbnailBuilder(user.avatar_url)
                                }
                            } : [.. topComponents]),
                            new Seperator(),
                            new TextDisplayBuilder(recentShifts.Count > 0 ? $"**{{strings.content.shiftadmin.recentshifts}}**:\n{recentShifts.ConvertAll(s => $"{types?.Find(t => t.id == s.type)?.name ?? "*{strings.errors.shiftadmin.unknowntype}*"} @ <t:{s.start_time.ToUnixTimeSeconds()}:R> {(s.end_time is not null ? $"{{strings.content.shiftadmin.for}} {Time.ConvertMillisecondsToString((s.end_time - s.start_time).Value.TotalMilliseconds, ", ", true, 60000)}" : "{strings.content.shiftadmin.untilnow}")}").Join("\n")}" : "{strings.errors.shiftadmin.norecentshifts}."),
                            new TextDisplayBuilder($"-# Type: {type?.name ?? "all"}")
                        ]
                    },
                    new ActionRowBuilder(
                        new ButtonBuilder
                        {
                            custom_id = $"sa_clockin {adminId} {userId} {type?.id}",
                            label = "{strings.button.shift.clockin}",
                            style = ButtonStyle.Success,
                            emoji = Strings.GetEmoji("shiftstart"),
                            disabled = data.currentShiftStart is not null
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_clockout {adminId} {userId} {type?.id}",
                            label = "{strings.button.shift.clockout}",
                            style = ButtonStyle.Danger,
                            emoji = Strings.GetEmoji("shiftstop"),
                            disabled = data.currentShiftStart is null
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_modify {adminId} {userId}",
                            label = "{strings.button.shiftadmin.modify}",
                            style = ButtonStyle.Primary,
                            emoji = Strings.GetEmoji("pen"),
                            disabled = data.totalCount == 0
                        }
                    ),
                    new ActionRowBuilder(
                        new ButtonBuilder
                        {
                            custom_id = $"sa_list {adminId} {userId} {type?.id ?? 0} 1",
                            label = "{strings.button.shiftadmin.listshifts}",
                            style = ButtonStyle.Secondary,
                            emoji = Strings.GetEmoji("folder"),
                            disabled = data.totalCount == 0
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_wipe {adminId} {userId} {type?.id}",
                            label = "{strings.button.shiftadmin.wipeshifts}",
                            style = ButtonStyle.Danger,
                            emoji = Strings.GetEmoji("delete"),
                            disabled = data.totalCount == 0
                        }
                    )
                ],
                flags = MessageFlags.IsComponentsV2
            };
        }

        private static List<Shift>? GetShifts(
            string guildId,
            string userId,
            ShiftType? type,
            int page
        )
        {
            int i = 3;

            var parameters = new List<object>
            {
                long.Parse(userId),
                long.Parse(guildId)
            };

            if (type is not null)
            {
                parameters.Add(type.id);
            }

            int offsetIndex = i++;
            parameters.Add((page - 1) * 5);

            string typeFilter = type is not null ? $" AND type = @{3}" : string.Empty;

            string query =
                $@"
                SELECT *
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2{typeFilter}
                ORDER BY start_time DESC
                LIMIT 5 OFFSET @{offsetIndex};
                ";

            return Postgres.Select<Shift>(query, parameters);
        }

        private static long GetShiftCount(string guildId, string userId, ShiftType? type)
        {
            var parameters = new List<object>
            {
                long.Parse(userId),
                long.Parse(guildId)
            };

            if (type is not null)
            {
                parameters.Add(type.id);
            }

            string typeFilter = type is not null ? " AND type = @3" : string.Empty;

            PostgresCount? countReq =
                Postgres.SelectFirst<PostgresCount>(
                $@"
                SELECT COUNT(*) AS count
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2{typeFilter}
                ",
                parameters
            );

            return countReq?.count ?? 0;
        }

        public static List<Component> GetShiftDisplays(List<Shift> shifts, List<ShiftType>? types)
        {
            List<Component> components = [];

            foreach (var shift in shifts)
            {
                long startSeconds = shift.start_time.ToUnixTimeSeconds();
                long? endSeconds = shift.end_time?.ToUnixTimeSeconds();
                long duration = (endSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - startSeconds;

                string endText = "{string.content.shiftadmin.notfinished}";
                if (endSeconds is not null)
                {
                    endText = $"<t:{endSeconds}:f>";
                }

                string idLine = $"`{shift.id}`";
                string startLine = $"**{{string.content.shiftadminlist.started}}**: <t:{startSeconds}:f>";
                string endLine = $"{{string.content.shiftadminlist.ended}}**: {endText}";
                string durationLine = $"**{{string.content.shiftadminlist.duration}}:** {Time.ConvertMillisecondsToString(duration, ", ", true, 60000)}";
                string typeLine = $"**{{string.content.shiftadminlist.type}}:** {types?.Find(t => t.id == shift.type)?.name ?? "unknown"}";

                components.Add(
                    new TextDisplayBuilder($"{idLine}\n{startLine}\n{endLine}\n{durationLine}\n{typeLine}")
                );
            }

            return components;
        }

        public static ActionRowBuilder GetPagination(
            string guildId,
            string adminId, 
            string userId, 
            ShiftType type, 
            int page
        )
        {
            long totalCount = GetShiftCount(guildId, userId, type);

            return new ActionRowBuilder
            {
                components = [
                    new ButtonBuilder
                    {
                        custom_id = $"sa_main {adminId} {userId} {type?.id}",
                        emoji = Strings.GetEmoji("back"),
                        style = ButtonStyle.Secondary
                    },
                    new ButtonBuilder
                    {
                        custom_id = $"sa_list {adminId} {userId} {type?.id ?? 0} {page - 1}",
                        emoji = Strings.GetEmoji("left"),
                        style = ButtonStyle.Primary,
                        disabled = page <= 1
                    },
                    new ButtonBuilder
                    {
                        custom_id = "null",
                        label = $"{page}/{Math.Ceiling((double)totalCount / 5)}",
                        style = ButtonStyle.Primary,
                        disabled = true
                    },
                    new ButtonBuilder
                    {
                        custom_id = $"sa_list {adminId} {userId} {type?.id ?? 0} {page + 1}",
                        emoji = Strings.GetEmoji("right"),
                        style = ButtonStyle.Primary,
                        disabled = page * 5 >= totalCount
                    }
                ]
            };
        }

        public static async Task<MessageBuilder> GetListMessage(string guildId, string userId, string adminId, ShiftType? type = null, int page = 1)
        {
            Task<User?> userTask = DiscordCache.Users.Get(userId);
            Task<List<ShiftType>?> typeTask = WhispCache.ShiftTypes.Get(guildId);

            List<Shift>? shifts = GetShifts(guildId, userId, type, page);

            if (shifts is null || shifts.Count == 0)
            {
                return new MessageBuilder
                {
                    content = "{ emoji.warning} {strings.errors.shiftadmin.failedgetdata}"
                };
            }

            User? user = await userTask;
            List<ShiftType>? types = await typeTask;

            List<Component> shiftDisplays = GetShiftDisplays(shifts, types);

            return new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [
                            new TextDisplayBuilder($"## {{strings.title.shiftadmin.list}}\n-# @{user?.username ?? "unknown"}"),
                            ..shiftDisplays,
                            new TextDisplayBuilder($"-# Type: {type?.name ?? "all"}")
                        ]
                    },
                    GetPagination(guildId, adminId, userId, type, page)
                    
                ],
                flags = MessageFlags.IsComponentsV2
            };
        }
        
        public static async Task<MessageBuilder> GetModifyMessage(Shift shift, string adminId)
        {
            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(shift.guild_id.ToString());

            return new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [
                            new TextDisplayBuilder($"## {{strings.title.shiftadmin.modify}}\n-# {shift.id}"),
                            new TextDisplayBuilder($"**{{strings.content.shiftadminmodify.started}}:** <t:{shift.start_time.ToUnixTimeSeconds()}:f>\n**{{strings.content.shiftadminmodify.ended}}:** <t:{shift.end_time?.ToUnixTimeSeconds()}:f>\n**{{strings.content.shiftadminmodify.duration}}:** {Time.ConvertMillisecondsToString(((shift.end_time ?? DateTimeOffset.UtcNow) - shift.start_time).TotalMilliseconds, ", ", true, 60000)}\n**{{strings.content.shiftadminmodify.type}}:** {types?.Find(t => t.id == shift.type)?.name ?? "unknown"}")
                        ]
                    },
                    new ActionRowBuilder(
                        new ButtonBuilder
                        {
                            custom_id = $"sa_main {adminId} {shift.moderator_id} {shift.type}",
                            style = ButtonStyle.Secondary,
                            emoji = Strings.GetEmoji("back")
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_addtime {adminId} {shift.id}",
                            label = "{strings.button.shiftadmin.addtime}",
                            style = ButtonStyle.Success,
                            emoji = Strings.GetEmoji("clockplus")
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_removetime {adminId} {shift.id}",
                            label = "{strings.button.shiftadmin.removetime}",
                            style = ButtonStyle.Danger,
                            emoji = Strings.GetEmoji("clockminus")
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_settime {adminId} {shift.id}",
                            label = "{strings.button.shiftadmin.settime}",
                            style = ButtonStyle.Primary,
                            emoji = Strings.GetEmoji("clockedit")
                        }
                    ),
                    new ActionRowBuilder(
                        new ButtonBuilder
                        {
                            custom_id = $"sa_changetype {adminId} {shift.id}",
                            label = "{strings.button.shiftadmin.changetype}",
                            style = ButtonStyle.Primary,
                            emoji = Strings.GetEmoji("pen")
                        },
                        new ButtonBuilder
                        {
                            custom_id = $"sa_delete {adminId} {shift.moderator_id} {shift.type} {shift.id}",
                            label = "{strings.button.shiftadmin.deleteshift}",
                            style = ButtonStyle.Danger,
                            emoji = Strings.GetEmoji("delete")
                        }
                    )
                ],
                flags = MessageFlags.IsComponentsV2
            };
        }
    }
}

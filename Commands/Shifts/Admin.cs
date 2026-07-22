using Discord;
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
using Whispbot.Tools.Disc;

namespace Whispbot.Commands.Shifts
{
    public class ShiftAdmin : Command
    {
        public override string Name => "Shift Admin";
        public override string Description => "Manage a user's shifts.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["shift", "admin"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to manage shifts for. If not provided, your own shifts will be shown.", CommandArgType.User, optional: true),
            new ("type", "The shift type to filter by. If not provided, all types will be shown.", CommandArgType.ShiftType, optional: true)
        ];
        public override List<string> Schema => ["<user:user?>", "<type:stype?>"];
        public override List<string> Aliases => ["shift admin"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ManageShifts)) return;

            IUser? userArg = ctx.args.Get("user")?.GetUser();
            IUser? user = userArg is not null ? userArg : ctx.User;
            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.invaliduser}");
                return;
            }

            List<ShiftType>? shiftTypes = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (shiftTypes is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.failed_get_types")}");
                return;
            }

            string? typeArg = ctx.args.Get("type")?.GetString();
            ShiftType? type = typeArg is not null ? shiftTypes.Find(t => t.triggers.Contains(typeArg) || t.id.ToString() == typeArg) : null;

            var message = await ShiftAdminMessages.GetMainMessage(ctx.GuildId, user.Id, ctx.UserId, type);
            await ctx.Reply(components: message, flags: MessageFlags.ComponentsV2);
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
        public static async Task<MessageComponent> GetMainMessage(ulong guildId, ulong userId, ulong adminId, ShiftType? type = null, Language lang = 0)
        {
            var userTask = Config.client!.GetUserAsync(userId, CacheMode.AllowDownload, RequestOptions.Default);
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
            ", [userId, guildId, ..(type is not null ? new List<ulong> { type.id } : [])]);

            if (data is null)
            {
                return new ComponentBuilderV2()
                    .WithTextDisplay("{emoji.warning} {string.errors.shiftadmin.failedgetdata}")
                    .Build();
            }

            float percent = MathF.Abs(MathF.Round(data.weeklyDurationIncreasePercent * 10f) / 10f);
            bool increase = data.weeklyDurationIncreasePercent > 0;
            bool decrease = data.weeklyDurationIncreasePercent < 0;

            List<ShiftType>? types = await typesTask;
            List<Shift> recentShifts = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Shift>>(data.recentShifts) ?? [];
            IUser? user = await userTask;

            List<TextDisplayBuilder> topComponents = [
                new TextDisplayBuilder($"## {"shifts.admin.title".Translate(lang)}\n-# @{user.Username}"),
                new TextDisplayBuilder(
                    $"**{"shifts.me.all_time".Translate(lang)}:** {data.totalCount} ({Time.ConvertMillisecondsToString(data.totalDuration * 1000, ", ", true, 60000, lang)})\n" +
                    $"**{"shifts.me.weekly".Translate(lang)}:** {data.weeklyCount} ({Time.ConvertMillisecondsToString(data.weeklyDuration * 1000, ", ", true, 60000, lang)})\n" +
                    $"**{"shifts.admin.trend".Translate(lang)}**: {$"shifts.admin.trend.{(increase ? "increase" : decrease ? "decrease" : "same")}".Translate(lang, percent.ToString())}"
                ),
            ];

            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithSection(topComponents, new ThumbnailBuilder(user.GetDisplayAvatarUrl()))
                        .WithSeparator()
                        .WithTextDisplay(recentShifts.Count > 0 ? 
                            $"**{"shifts.admin.recent_shifts".Translate(lang)}**:\n{recentShifts.ConvertAll(s => 
                                $"{types?.Find(t => t.id == s.type)?.name ?? $"*{"phrase.unknown".Translate(lang)}*"} @ <t:{s.start_time.ToUnixTimeSeconds()}:R> {(s.end_time is not null ? $"{"phrase.for".Translate(lang)} {Time.ConvertMillisecondsToString((s.end_time - s.start_time).Value.TotalMilliseconds, ", ", true, 60000
                            )}" :
                            "{string.content.shiftadmin.untilnow}")}").Join("\n")}" : "{string.errors.shiftadmin.norecentshifts}.")
                        .WithTextDisplay($"-# Type: {type?.name ?? "all"}")
                        .WithAccentColor(data.currentShiftStart is not null ? new Color(0, 150, 0) : null)
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton("shifts.button.clockin".Translate(lang), $"sa_clockin {adminId} {userId} {type?.id}", ButtonStyle.Success, Emojis.Get("shiftstart"), disabled: data.currentShiftStart is not null)
                        .WithButton("shifts.button.clockout".Translate(lang), $"sa_clockout {adminId} {userId} {type?.id}", ButtonStyle.Danger, Emojis.Get("shiftstop"), disabled: data.currentShiftStart is null)
                        .WithButton("shifts.admin.button.modify".Translate(lang), $"sa_modify {adminId} {userId}", ButtonStyle.Primary, Emojis.Get("pen"), disabled: data.totalCount == 0)
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton("shifts.admin.button.list".Translate(lang), $"sa_list {adminId} {userId} {type?.id ?? 0} 1", ButtonStyle.Secondary, Emojis.Get("folder"), disabled: data.totalCount == 0)
                        .WithButton("shifts.admin.button.wipe".Translate(lang), $"sa_wipe {adminId} {userId} {type?.id}", ButtonStyle.Danger, Emojis.Get("delete"), disabled: data.totalCount == 0)
                )
                .Build();
        }

        public static async Task<MessageComponent> GetListMessage(ulong guildId, ulong userId, ulong adminId, ShiftType? type = null, int page = 1)
        {
            var userTask = Config.client!.GetUserAsync(userId, CacheMode.AllowDownload, RequestOptions.Default);
            var typeTask = WhispCache.ShiftTypes.Get(guildId);
            int i = 3;

            List<Shift>? shifts = Postgres.Select<Shift>(@"
                SELECT *
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2" + (type is not null ? $" AND type = @{i++}" : "") + @$"
                ORDER BY start_time DESC
                LIMIT 5 OFFSET @{i++};
            ", [userId, guildId, .. (type is not null ? new List<ulong> { type.id } : []), (page - 1) * 5]);
            PostgresCount? countReq = Postgres.SelectFirst<PostgresCount>(@"
                SELECT COUNT(*) AS count
                FROM shifts
                WHERE moderator_id = @1 AND guild_id = @2" + (type is not null ? " AND type = @3" : ""),
                [userId, guildId, .. (type is not null ? new List<ulong> { type.id } : [])]
            );
            long totalCount = countReq?.count ?? 0;

            if (shifts is null || shifts.Count == 0)
            {
                return new ComponentBuilderV2()
                    .WithTextDisplay("{emoji.warning} {string.errors.shiftadmin.failedgetdata}")
                    .Build();
            }

            IUser? user = await userTask;
            List<ShiftType>? types = await typeTask;

            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"## {{string.title.shiftadmin.list}}\n-# @{user?.Username ?? "unknown"}")
                        .WithComponents(shifts.SelectMany<Shift, TextDisplayBuilder>(s => [
                            new TextDisplayBuilder($"`{s.id}`\n**{{string.content.shiftadminlist.started}}**: <t:{s.start_time.ToUnixTimeSeconds()}:f>\n**{{string.content.shiftadminlist.ended}}**: {(s.end_time is not null ? $"<t:{s.end_time.Value.ToUnixTimeSeconds()}:f>" : "{string.content.shiftadmin.notfinished}")}\n**{{string.content.shiftadminlist.duration}}:** {Time.ConvertMillisecondsToString(((s.end_time ?? DateTimeOffset.UtcNow) - s.start_time).TotalMilliseconds, ", ", true, 60000)}\n**{{string.content.shiftadminlist.type}}:** {types?.Find(t => t.id == s.type)?.name ?? "unknown"}")
                        ]))
                        .WithTextDisplay($"-# Type: {type?.name ?? "all"}")
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton("{string.button.shiftadmin.back}", $"sa_main {adminId} {userId} {type?.id}", ButtonStyle.Secondary, Emojis.Get("back"))
                        .WithButton("{string.button.shiftadmin.previous}", $"sa_list {adminId} {userId} {type?.id ?? 0} {page - 1}", ButtonStyle.Primary, Emojis.Get("left"), disabled: page <= 1)
                        .WithButton($"{page}/{Math.Ceiling((double)totalCount / 5)}", "null", ButtonStyle.Primary, disabled: true)
                        .WithButton("{string.button.shiftadmin.next}", $"sa_list {adminId} {userId} {type?.id ?? 0} {page + 1}", ButtonStyle.Primary, Emojis.Get("right"), disabled: page * 5 >= totalCount)
                )
                .Build();
        }
        
        public static async Task<MessageComponent> GetModifyMessage(Shift shift, string adminId)
        {
            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(shift.guild_id);

            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"## {{string.title.shiftadmin.modify}}\n-# {shift.id}")
                        .WithTextDisplay($"**{{string.content.shiftadminmodify.started}}:** <t:{shift.start_time.ToUnixTimeSeconds()}:f>\n**{{string.content.shiftadminmodify.ended}}:** <t:{shift.end_time?.ToUnixTimeSeconds()}:f>\n**{{string.content.shiftadminmodify.duration}}:** {Time.ConvertMillisecondsToString(((shift.end_time ?? DateTimeOffset.UtcNow) - shift.start_time).TotalMilliseconds, ", ", true, 60000)}\n**{{string.content.shiftadminmodify.type}}:** {types?.Find(t => t.id == shift.type)?.name ?? "unknown"}")
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton($"sa_main {adminId} {shift.moderator_id} {shift.type}", "{string.button.shiftadmin.back}", ButtonStyle.Secondary, Emojis.Get("back"))
                        .WithButton($"sa_addtime {adminId} {shift.id}", "{string.button.shiftadmin.addtime}", ButtonStyle.Success, Emojis.Get("clockplus"))
                        .WithButton($"sa_removetime {adminId} {shift.id}", "{string.button.shiftadmin.removetime}", ButtonStyle.Danger, Emojis.Get("clockminus"))
                        .WithButton($"sa_settime {adminId} {shift.id}", "{string.button.shiftadmin.settime}", ButtonStyle.Primary, Emojis.Get("clockedit"))
                )
                .WithActionRow(
                    new ActionRowBuilder()
                        .WithButton($"sa_changetype {adminId} {shift.id}", "{string.button.shiftadmin.changetype}", ButtonStyle.Primary, Emojis.Get("pen"))
                        .WithButton($"sa_delete {adminId} {shift.moderator_id} {shift.type} {shift.id}", "{string.button.shiftadmin.deleteshift}", ButtonStyle.Danger, Emojis.Get("delete"))
                )
                .Build();
        }
    }
}

